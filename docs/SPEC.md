# Project Spec

## Services
- auth-api
- resource-api
- web-app

## Endpoints (Summary)
Public/Player:
- GET /packs?random=true
- GET /puzzles/next?packId=...
- POST /game/submit
- GET /profile/progress

Admin CMS:
- GET/POST/PUT/DELETE /cms/images
- GET/POST/DELETE /cms/tags
- POST /cms/images/{id}/tags
- DELETE /cms/images/{id}/tags/{tag}
- GET/POST/PUT/DELETE /cms/packs
- GET/POST/PUT/DELETE /cms/puzzles
- POST /cms/packs/{id}/publish

## Randomization Rules
- Shuffle published packs when `?random=true`.
- Randomize puzzles within pack and exclude recently solved.
- If all solved, cycle with a new order or show pack completed.

## Gameplay Loop
- Choose pack from randomized list
- See 4 images, input guess
- Submit -> feedback + next puzzle

## CMS Capabilities
Images, Tags, Puzzles, Packs with admin-only access.
