# Solarium

Prototipo 2D procedural para Unity 6 en el que una criatura aprende con PPO a
comer, administrar energía, evitar enemigos, curarse, cruzar barro y evitar
lava y otros peligros.

## Abrir

1. Abre el proyecto con Unity `6000.4.10f1`.
2. Si las escenas no aparecen tras importar, usa
   `Tools > Solarium > Create or Repair Project`.
3. Abre `Scenes/SolariumDemo` para jugar con WASD/flechas y Shift.
4. Abre `Scenes/SolariumTraining` para entrenar seis arenas en paralelo.
5. Abre `Scenes/SolariumSurvival` para observar al modelo final en un único
   mapa procedural de supervivencia. Esta escena usa
   `Models/Solarium.onnx` en modo inferencia: no necesita Python ni el trainer.

Si la escena de supervivencia no aparece aún, Unity debe importar primero el
modelo ONNX. Después usa `Tools > Solarium > Create or Repair Survival
Showcase`.

La documentación completa está en
`Training/README_TRAINING.md`.

## Persistent memory v3

La versión v2 añade una acción `Interactuar`: Sol puede recoger un fragmento
morado con E, llevar uno a la vez, depositarlo en el refugio turquesa y usar la
reserva como energía de emergencia. Los hunters retroceden mientras Sol está en
el refugio. Su red LSTM recuerda contexto reciente de peligros y recursos.
Esta versión tiene una red distinta de Sol v1 y debe entrenarse con un
`run-id` nuevo: `solarium-persistent-memory-v3`.
