# Town Square POI

Use this when the intent is "go to town", "town square", "fountain", "be visible in public", or a central social meeting point.

## Mechanical Target

`target(locationName=Town,x=42,y=17,source=map-skill:stardew.navigation.poi.town-square)`

## Use

- Good for social visibility, public waiting, and casual town observation.
- After emitting this target, the parent must let the host/local executor run it and then monitor `stardew_task_status`.
