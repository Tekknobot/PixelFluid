DESYNCHRONIZED SURFER JUMPS + NO UI

- Removed the OnGUI title, status message, controls, ride score and best-score panel.
- No runtime UI is drawn by BeachGameController.
- Each generated surfer now receives a different initial layer-jump delay.
- Each surfer also receives a slightly different base transfer interval.
- Every layer transfer schedules a newly randomized interval.
- Surfers therefore jump between simulation layers independently instead of all jumping together.
- Z camera cycling and Escape camera restore remain unchanged.
