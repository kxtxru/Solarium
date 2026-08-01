# Solarium 3D

Versión 3D isométrica y low-poly de Solarium. Conserva el contrato de la
política recurrente: 194 observaciones, dos acciones continuas, dos ramas
discretas y PPO con LSTM de memoria 128 / secuencia 32.

## Mundo infinito

`SolariumSurvival3D` usa un mundo determinista por chunks de 24 metros. Solo
mantiene cargada una ventana de 3 × 3, pero conserva en disco los estados de
las zonas visitadas, alimentos, cooldowns y raciones almacenadas. La partida
se guarda en `Application.persistentDataPath/Solarium3D/world-v1.json` y Sol
reaparece en el último santuario visitado.

Los cuatro biomas son pradera, bosque, humedal y zona volcánica. Quitar objetos
de memoria no modifica su estado: al volver a un chunk se reconstruye desde la
seed y se aplican sus cambios guardados. El botón `NUEVO MUNDO` del HUD pide
confirmación antes de sustituir la única partida automática.

Las barras muestran vida y energía numéricas. Los recursos magenta se presentan
como raciones; al depositarlas, el santuario muestra frutos y un contador `×N`.

## Crear o reparar

En Unity 6000.4.10f1 usa `Tools > Solarium 3D > Create or Repair Project`.
El comando genera de forma reproducible:

- `Scenes/SolariumSurvival3D`: showcase con ONNX, cámara, HUD, audio y efectos.
- `Scenes/SolariumTraining3D`: seis arenas independientes sin renderizado.
- paleta, materiales URP y volumen de postprocesado.

La escena 2D permanece en `Assets/Solarium` como referencia durante la
validación. Los objetos del mundo 3D se reciclan entre episodios mediante
pooling; no existe un límite fijo de 120 segundos.

La física usa capas dedicadas: `SolariumGameplay3D`, `SolariumGround3D`,
`SolariumVisual3D` y `SolariumEffects3D`. Los rayos de observación solo incluyen
jugabilidad y suelo, por lo que vegetación, partículas y decoración no alteran
el contrato ni el comportamiento aprendido.

### Modelos

El showcase usa `Models/SolariumPersistentMemoryV3.onnx`, promovido sin
sobrescribir el run original de 5,2 millones de pasos. Su contrato real es 194
observaciones, máscaras `[4]`, acciones continuas `[2]`, ramas discretas
`[2, 2]` y LSTM con `recurrent_in/out` de 128.

El antiguo `Assets/Solarium/Models/Solarium.onnx` tiene 187 observaciones, una
rama discreta y `memory_size=0`. Se conserva un fallback reproducible en
`Models/SolariumLegacy194.onnx`:

```powershell
.\.venv\Scripts\python.exe Assets/Solarium3D/Training/adapt_legacy_onnx.py `
  Assets/Solarium/Models/Solarium.onnx `
  Assets/Solarium3D/Models/SolariumLegacy194.onnx
```

El fallback selecciona las 187 señales antiguas dentro del contrato nuevo y
mantiene interacción en cero. El modelo que exporte `solarium-3d-v1` sustituirá
al V3 solamente después de superar la evaluación sobre las mismas 50 seeds.

## Observar

Abre `Scenes/SolariumSurvival3D` y pulsa Play. La cámara sigue a Sol:

- `Q` / `E`: rotar.
- rueda del ratón: zoom.
- HUD: pausa y velocidades 1x, 2x y 5x.

No hay control jugable de Sol en el producto. El modo heurístico se conserva
solo para depuración cuando no se puede cargar el ONNX.

## Entrenar

La primera ejecución 3D usa un run nuevo e importa únicamente los pesos del
run 2D compatible; nunca sobrescribe sus resultados:

```powershell
.\.venv\Scripts\mlagents-learn.exe Assets/Solarium3D/Training/solarium_ppo_3d.yaml `
  --run-id=solarium-3d-v1 `
  --initialize-from=solarium-persistent-memory-v3
```

El ajuste específico del mundo infinito usa seis streams efímeros independientes
y el run reservado `solarium-infinite-v1`:

```powershell
.\.venv\Scripts\mlagents-learn.exe Assets/Solarium3D/Training/solarium_ppo_infinite.yaml `
  --run-id=solarium-infinite-v1 `
  --initialize-from=solarium-persistent-memory-v3
```

La fase estratégica conserva ese checkpoint y especializa la política en
recoger raciones, depositarlas y volver a los refugios cuando baja la energía:

```powershell
.\.venv\Scripts\mlagents-learn.exe Assets/Solarium3D/Training/solarium_ppo_infinite.yaml `
  --run-id=solarium-strategy-v1 `
  --initialize-from=solarium-infinite-v1
```

Los episodios de `InfiniteTraining` se segmentan cada 300 segundos simulados
como interrupciones (no como muertes), de modo que PPO conserva el valor futuro
y ML-Agents puede publicar recompensa media y longitud de episodio.

El nuevo ONNX no sustituye a `SolariumPersistentMemoryV3.onnx` hasta superar la
evaluación determinista de supervivencia, cambios de chunk, retornos y consumo
de reservas. Las métricas se publican como `Solarium/Chunks Discovered`,
`Solarium/Chunks Revisited` y `Solarium/Rations *`.

Espera `Listening on port 5004`, abre `SolariumTraining3D` y pulsa Play.
Para reanudar la misma ejecución:

```powershell
.\.venv\Scripts\mlagents-learn.exe Assets/Solarium3D/Training/solarium_ppo_3d.yaml `
  --run-id=solarium-3d-v1 --resume
```

Smoke test reproducible desde la build de entrenamiento:

```powershell
.\.venv\Scripts\mlagents-learn.exe Assets/Solarium3D/Training/solarium_ppo_3d_smoke.yaml `
  --env Builds/SolariumTraining3D/SolariumTraining3D.exe `
  --run-id solarium-3d-smoke --no-graphics --force
```

La evaluación comparativa usa 50 seeds deterministas:
`12345 + índice * 7919`, con índice de 0 a 49. Se comparan supervivencia
mediana, comida por minuto, recompensa y causas de muerte con el checkpoint 2D.

## Builds

- `Tools > Solarium 3D > Build Windows Training Player`
- `Tools > Solarium 3D > Build Windows Showcase`

El player de entrenamiento se genera en `Builds/SolariumTraining3D` y admite
`--no-graphics`. El showcase se genera en `Builds/Solarium3D`.
