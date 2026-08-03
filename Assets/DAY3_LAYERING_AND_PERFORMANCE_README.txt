DAY 3 LAYERING AND PERFORMANCE PATCH

- Shadow Surfer is assigned to the player current InterWave lane immediately and whenever the lane changes.
- Shadow uses the player sorting layer and renders one local order behind the player, while still respecting water lane queues.
- Day 3 does not add water simulations or increase simulation grid resolution.
- Water corruption writes are limited to 8 updates per second instead of every rendered frame.
- Reflection FieldInfo values are cached once instead of looked up repeatedly.
- Returning water threats are capped at 5 and spawn cadence is slightly reduced.
