# PANGAEA

A persistent, open-world, anime-style social survival MMO for mobile and web.

## Vision

PANGAEA is not a traditional battle royale or grind MMO. It's a living virtual world where:
- **Geography matters** - Spawn near your real-world location
- **Identity matters** - AI-generated anime avatars from your photos
- **Reputation matters** - Karma system tracks your actions
- **Other players are the content** - Emergent social gameplay

## Core Features

### Geo-Based Spawning
- First spawn within 10km of your real location
- Home location is permanent - creates regional communities
- Entire world is one merged supercontinent (Pangaea)

### Social Systems
- **Proximity Voice Chat** - Always-on, distance-based volume
- **Karma/Reputation** - Visual name colors (Bandit → Guardian)
- **Bounty System** - Player-placed gold bounties on killers
- **Clans** - 20-player cap with alliance system

### Survival & Combat
- No guns - swords, spears, bows only
- Hunger, health, stamina management
- Death drops all gear (cosmetics are soulbound)
- Levels 1-10 with attribute points

### Base Building
- Walls, storage, crafting stations
- **Offline Raid Protection** - 90% damage reduction when owner offline
- Territory control for clans

## Tech Stack

- **Engine**: Unity 2022.3 LTS (URP)
- **Backend**: Firebase (Auth, Firestore, Cloud Functions)
- **Platforms**: iOS, Android, WebGL

## Project Structure

```
PANGAEA/
├── Assets/
│   └── Scripts/
│       ├── Core/           # GameManager, PlayerManager, WorldManager
│       ├── Player/         # PlayerController, Stats, Input
│       ├── Combat/         # Combat system, weapons
│       ├── Inventory/      # Items, equipment, crafting
│       ├── Building/       # Base building system
│       ├── Social/         # Voice chat, clans, bounties
│       ├── Networking/     # Network sync, Firebase
│       ├── World/          # Geo spawning, world events
│       └── UI/             # HUD, menus
├── Server/
│   └── functions/          # Firebase Cloud Functions
└── Docs/
```

## Getting Started

### Prerequisites
- Unity 2022.3 LTS or newer
- Firebase account
- Node.js (for Firebase Functions)

### Setup
1. Clone this repository
2. Open in Unity Hub
3. Import Firebase SDK packages
4. Add your `google-services.json` / `GoogleService-Info.plist`
5. Deploy Firebase rules: `firebase deploy --only firestore:rules`

## Development Status

### Implemented
- [x] Core game architecture
- [x] Player movement & isometric camera
- [x] Stats system (health, stamina, hunger)
- [x] Inventory with weight limits
- [x] Combat system (melee/ranged)
- [x] Building placement system
- [x] Karma/reputation system
- [x] Clan system
- [x] Proximity voice foundation
- [x] Geo-spawn system

### In Progress
- [ ] Firebase integration
- [ ] Unity scene setup
- [ ] Player prefabs
- [ ] UI implementation

### Planned
- [ ] Multiplayer sync
- [ ] World events
- [ ] Crafting recipes
- [ ] Taming system
- [ ] Vehicles

## License

Proprietary - All rights reserved

## Contact

Project Lead: Daly
