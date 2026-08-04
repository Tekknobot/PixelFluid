TURTLE COMPONENT FIX

- RequireComponent attributes are placed only above class declarations.
- GiantTurtleSwimmer safely creates/configures:
  Rigidbody2D, BoxCollider2D, SpriteRenderer, InterWaveRenderItem.
- SeaTurtleSwimmer safely creates/configures:
  Rigidbody2D, CircleCollider2D, SpriteRenderer, InterWaveRenderItem.
- Giant turtle uses the correct renderer2D field.
- DayThreeTurtleDirector adds colliders before swimmer scripts as extra safety.
