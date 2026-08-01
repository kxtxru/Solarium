# Entrenamiento de Solarium en Windows

## Versiones fijadas

Este proyecto usa Unity `6000.4.10f1`, `com.unity.ml-agents` `4.0.0`,
Python `3.10.11` (64 bits) y `mlagents` `1.1.0`. ML-Agents admite Python
3.10.1-3.10.12; en Windows se usa 3.10.11 porque fue la última versión 3.10
con instalador oficial. Unity Package 4.0.0 y Python Package 1.1.0 forman
ML-Agents Release 23; el paquete de Unity declara Unity 6000.0 como mínimo.

Compruébalo desde la raíz del proyecto:

```powershell
Get-Content ProjectSettings\ProjectVersion.txt
Select-String Packages\manifest.json -Pattern "ml-agents"
py -3.10 --version
```

Tras instalar Python:

```powershell
mlagents-learn --help
python -m pip show mlagents mlagents-envs
```

## Preparar el proyecto

Abre la carpeta del proyecto en Unity Hub con `6000.4.10f1`. La primera
importación crea automáticamente las escenas. También puedes regenerarlas con
`Tools > Solarium > Create or Repair Project`.

- `SolariumTraining`: seis arenas, mismo Behavior Name `Solarium`, sin cámara.
- `SolariumDemo`: una arena, cámara, HUD, gráfica y control manual.

Los raycasts están implementados en `SolObservations`: 12 rayos, distancia
normalizada y propiedades estables: sólido, valor de recurso, curación,
hostilidad, daño, ralentización, letalidad, movimiento y peligro ambiental.
Cada rayo reserva además cuatro canales para futuras mecánicas. Junto con el
estado interno forman exactamente 194 entradas y evitan una configuración
manual frágil de `RayPerceptionSensor`.

## Entorno Python

Instala Python 3.10.11 x64. En PowerShell:

```powershell
py -3.10 -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install --upgrade pip
python -m pip install -r Assets/Solarium/Training/requirements-mlagents.txt
mlagents-learn --help
```

Si PowerShell bloquea la activación, abre CMD:

```bat
.venv\Scripts\activate.bat
python -m pip install --upgrade pip
python -m pip install -r Assets\Solarium\Training\requirements-mlagents.txt
```

Unity recomienda en Windows instalar PyTorch explícitamente si se quiere CUDA.
Para este pequeño modelo vectorial empieza con la instalación normal: la CPU
suele ser suficiente y evita dedicar memoria de la GTX 1080 sin una mejora
medida.

El archivo de requisitos fija PyTorch 2.2.1, ONNX 1.15 y NumPy 1.23.5: son las
versiones compatibles con ML-Agents 1.1.0 y su exportador ONNX. No instales
`onnxscript` en este entorno; pertenece al exportador nuevo de PyTorch y causa
el error al guardar checkpoints con esta versión de ML-Agents.

## Primera prueba manual

Abre `Assets/Solarium/Scenes/SolariumDemo.unity` y pulsa Play. El agente de esta
escena comienza en `Heuristic Only`:

- WASD o flechas: movimiento.
- Shift: sprint.
- E: interactuar (recoger fragmento, depositarlo en el refugio o usar reserva).
- HUD: seed, vida, energía, recompensa, dificultad y botones de reinicio.
- Selecciona a `Sol` y activa `Show Debug` para ver sensores y última acción.

La diagonal está normalizada; Shift aumenta velocidad y consumo. La comida
verde/dorada da energía, las cruces azules curan, los triángulos persiguen, las
manchas moradas dañan y consumen energía, el barro marrón ralentiza y los
diamantes naranja-rojos de lava terminan el episodio al tocarlos.

## Entrenar desde el Editor

1. Abre `Assets/Solarium/Scenes/SolariumTraining.unity`.
2. Asegúrate de que `Behavior Type` es `Default` en los seis agentes.
3. Con Unity detenido, ejecuta:

```powershell
.\.venv\Scripts\Activate.ps1
mlagents-learn Assets/Solarium/Training/solarium_ppo.yaml --run-id=solarium-v1
```

4. Espera a ver `Listening on port 5004`.
5. Pulsa Play en Unity.
6. La consola debe indicar que se conectó el comportamiento `Solarium?team=0`.

Detén primero Play y después el entrenador con Ctrl+C para que exporte el ONNX.

Solarium Persistent memory v3 tiene 194 observaciones, dos acciones discretas
(`sprint` e `interactuar`). El modelo Sol v1 de 187 entradas no es compatible:
empieza con un `run-id` nuevo; no uses `--resume` ni `--initialize-from` con
ejecuciones v1.

```powershell
mlagents-learn Assets/Solarium/Training/solarium_ppo.yaml --run-id=solarium-persistent-memory-v3
```

El contrato de 194 entradas está diseñado para permanecer fijo. Añade nuevas
entidades mapeando propiedades existentes —por ejemplo, hielo como
ralentización o una mina como daño + letalidad—. Antes de añadir una observación
nueva, usa uno de los cuatro canales reservados por rayo; así los checkpoints
futuros seguirán siendo compatibles. Una acción nueva sí cambia la arquitectura
y requiere entrenar un modelo nuevo.

Reanudar exactamente la misma ejecución:

```powershell
mlagents-learn Assets/Solarium/Training/solarium_ppo.yaml --run-id=solarium-persistent-memory-v3 --resume
```

La primera ejecución de Persistent memory v3 llega a 12 millones de pasos. Reanuda
con el mismo comando para seguir afinando la versión v2.

Para empezar desde cero usa otro identificador, por ejemplo
`--run-id=solarium-v2`. No reutilices un directorio existente sin `--resume`;
así no sobrescribes resultados accidentalmente.

## Entrenar con una build

En Unity usa `Tools > Solarium > Build Windows Training Player`. Genera
`Builds/SolariumTraining/SolariumTraining.exe`.

Con un Ryzen 5 3600 y 16 GB comienza con una instancia de la build (cada
instancia ya contiene cuatro arenas):

```powershell
mlagents-learn Assets/Solarium/Training/solarium_ppo.yaml `
  --run-id=solarium-build-v1 `
  --env=Builds/SolariumTraining/SolariumTraining.exe `
  --no-graphics
```

Si CPU y RAM tienen margen, prueba dos procesos:

```powershell
mlagents-learn Assets/Solarium/Training/solarium_ppo.yaml `
  --run-id=solarium-build-v2 `
  --env=Builds/SolariumTraining/SolariumTraining.exe `
  --no-graphics `
  --num-envs=2
```

No empieces por más de 2. Mide pasos por segundo y uso de RAM; más entornos no
siempre producen más muestras por segundo.

## TensorBoard

En otra terminal con el entorno activado:

```powershell
tensorboard --logdir results
```

Abre `http://localhost:6006`.

- `Cumulative Reward`: debe mejorar también en seeds nuevas, no solo subir.
- `Episode Length`: debe crecer sin que la política se limite a quedarse quieta.
- `Policy Loss`: oscila; una caída suave es normal, explosiones repetidas no.
- `Value Loss`: debe tender a estabilizarse; valores enormes sugieren recompensas
  demasiado bruscas.
- `Entropy`: debería bajar lentamente. Si cae antes de aprender, sube `beta`; si
  permanece alta y el agente deambula, bájala.
- Métricas `Solarium/*`: supervivencia, comida, curación, daño y tasa de muerte
  registradas por C#.

Hay aprendizaje real cuando recompensa, comida y supervivencia mejoran a la vez
en evaluación determinista. Una política estancada suele mostrar recompensa
plana, entropía casi constante y comida media próxima a una política aleatoria.

## Recompensas y conductas degeneradas

- Supervivencia es diminuta; si Sol se queda quieto, aumenta la penalización
  `stuckPenaltyPerSecond` o reduce `survivalRewardPerSecond`.
- Comida usa una recompensa moderada para que decenas de recogidas no oculten
  daño, muerte, muros o curación.
- Daño y muerte son negativos; si se suicida para cambiar de mapa, haz
  `deathPenalty` más negativa.
- Sprint tiene coste por segundo; si corre siempre, aumenta en magnitud
  `sprintPenaltyPerSecond`.
- Curación solo recompensa salud realmente recuperada. Entrar con salud completa
  da cero y no consume el cooldown.
- Empujar contra muros recibe una penalización limitada por intervalo; el barro
  reduce la velocidad y tiene un coste diminuto por segundo.
- La lava no produce daño gradual: termina el episodio con causa `lava`.
- No existe recompensa por acercarse, así que no puede cobrar quedándose junto a
  comida.
- Permanecer sin desplazamiento activa `stuckPenaltyPerSecond`, útil contra
  paredes, círculos y políticas inmóviles.

Cada componente se acumula por separado en `RewardBreakdown`. Cada episodio se
escribe, no cada frame, en:

`%USERPROFILE%\AppData\LocalLow\<CompanyName>\<ProductName>\SolariumStats\episodes.csv`

El HUD muestra la ruta real.

## Usar el modelo entrenado

El modelo final aparece en:

`results\solarium-v1\Solarium.onnx`

1. Arrástralo a `Assets/Solarium/Models/` (puedes crear esa carpeta).
2. Abre `SolariumDemo`.
3. Selecciona `DemoArena/Sol`.
4. En `Behavior Parameters`, asigna el ONNX a `Model`.
5. Cambia `Behavior Type` a `Inference Only` (o usa `Default` sin entrenador).
6. Pulsa Play y prueba varias seeds desde el HUD.

Para comparar un checkpoint anterior, asigna su ONNX, usa las mismas 50 seeds y
guarda una copia del CSV con el nombre del modelo. Repite con una política
aleatoria, una sin entrenar y el modelo final. Resume los resultados:

```powershell
python Assets/Solarium/Training/summarize_evaluation.py `
  random.csv untrained.csv checkpoint-old.csv trained.csv
```

El resumen informa supervivencia, comida, curación, daño, tasa total de muerte y
muertes por lava sobre los últimos 50 episodios.

## Currículum y experimentos

El YAML avanza por recompensa reciente suavizada: comer y evadir barro y lava;
obstáculos bajo presión; peligro; administración de riesgo; y supervivencia
procedural. Todas las etapas incluyen al menos un cazador, una zona de barro y
una zona de lava; más adelante aumentan enemigos, terrenos y peligro tóxico.
Al morir, `EndEpisode` provoca un respawn inmediato con una seed y una
distribución nuevas. Para evaluar o volver a una etapa, activa
`Use Manual Difficulty` en el `DifficultyController` y fija un valor de 0 a 1.
El currículum nunca sobrescribe ese valor mientras el modo manual esté activo.

Primer experimento recomendado: entrena hasta superar de forma estable la
lección `EatEvadeMudAndLava`, evalúa 50 seeds a dificultad 0 y compáralo con
`Default` sin ONNX. Comprueba conjuntamente supervivencia, comida, curación y
tasa de muerte; una política que solo huye no ha resuelto la tarea.
