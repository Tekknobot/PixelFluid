SURFER SLUG - BOSS THROW PRIORITY PATCH

Changed:
- Active Godzilla encounters take priority over ordinary sea hazards when throwing.
- The closest active Godzilla is selected before sharks, squid, or jellyfish.
- While a projectile is locked to Godzilla, ordinary creature trigger colliders are ignored.
- Godzilla damage and ricochet are handled by SodaCanProjectile to prevent duplicate damage or absorption.
- If no active boss exists, normal nearest-hazard targeting remains unchanged.
