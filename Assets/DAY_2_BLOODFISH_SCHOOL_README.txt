DAY 2 BLOODFISH SCHOOL PATCH

Added a Day 2-only Bloodfish school encounter using the supplied 32x32 move and attack sheets.

New scripts:
- BloodfishSchoolController.cs
- BloodfishSchoolSpawner.cs
- BloodfishSwimmer.cs

Behaviour:
- Schools spawn in varied formations and travel styles.
- They detect and pursue nearby surfers.
- Individual fish switch from move to attack animation near the target.
- Contact damages the surfer using the existing hazard response.
- Thrown items target Bloodfish, hit them, play the existing impact sound, remove an individual fish, and ricochet exactly like Jellyfish.
- Defeated schools respawn after a delay and follow endless-section recycling.

Progression:
- Day 1 keeps Jellyfish encounters.
- Day 2 begins with one Bloodfish school.
- Strange Tide spawns two Bloodfish schools.
- Storm spawns three Bloodfish schools.

The project was not compiled in Unity in this environment. Let Unity import the new scripts/resources and report any compiler errors if they appear.
