# PANGAEA - Technical Architecture

## Project Structure

```
PANGAEA/
├── Assets/
│   ├── Scripts/
│   │   ├── Core/           # Game managers and core systems
│   │   ├── Player/         # Player controller, stats, input
│   │   ├── Combat/         # Combat system, weapons, projectiles
│   │   ├── Survival/       # Health, hunger, stamina
│   │   ├── Inventory/      # Items, equipment, crafting
│   │   ├── Building/       # Base building, structures
│   │   ├── Social/         # Voice chat, clans, bounties
│   │   ├── Networking/     # Network manager, sync
│   │   ├── World/          # Geo spawning, world events
│   │   ├── UI/             # User interface
│   │   ├── Data/           # ScriptableObjects, configs
│   │   └── Utils/          # Helpers, constants
│   ├── Prefabs/
│   ├── ScriptableObjects/
│   ├── Art/
│   ├── Audio/
│   ├── Scenes/
│   └── Resources/
├── Server/
└── Docs/
```

## Core Systems

### GameManager (Singleton)
Central game state controller. Manages:
- Game state transitions (Menu → Connecting → Playing)
- Reference to all major managers
- Application lifecycle events

### PlayerManager
Tracks all players (local and remote):
- Player registration/unregistration
- Nearest player queries
- Spawn position calculation

### WorldManager
Manages the game world:
- Chunk loading/unloading
- Population density zones
- Day/night cycle
- Weather system

### NetworkManager
Handles multiplayer:
- Connection management
- Message queue
- Player synchronization
- Combat validation (server-authoritative)

## Player Systems

### PlayerController
Main player component:
- Input handling (mobile/desktop)
- Movement (isometric)
- PvP mode toggle
- Interaction system

### PlayerStats
Progression and vitals:
- Levels 1-10 with attribute points
- Health/Stamina/Hunger
- Karma/Reputation
- Profession system (locked choice)

### PlayerInventory
PUBG-style inventory:
- Weight-based capacity
- Equipment slots
- Quick slots
- Drop on death (except soulbound)

## Combat System

### PlayerCombat
Melee and ranged combat:
- Auto-targeting
- Combo system (3-hit chains)
- Stamina costs
- Finisher animations

### Weapons (No Guns)
- Swords, knives, spears
- Bows and thrown weapons
- Durability system
- Attribute requirements

## Building System

### BuildingSystem
Base construction:
- Grid-based placement
- Snap points between pieces
- Resource costs
- Preview validation

### BuildingHealth
Structure durability:
- Damage system
- Offline raid protection (90% reduction when owner offline)
- Repair mechanics
- Salvage on destruction

## Social Systems

### ProximityVoiceChat
Always-on voice:
- Distance-based volume
- Push-to-talk optional
- Microphone input processing
- Per-player audio sources

### ClanSystem
Player organizations:
- 20 player cap
- Ranks (Member/Officer/Leader)
- Alliance system (max 3)
- Friendly fire prevention

### BountySystem
Player-driven justice:
- Place gold bounties on killers
- Auto-bounty for negative karma
- Map marker for high bounties
- Rewards for claiming

## World Systems

### GeoSpawnSystem
Location-based spawning:
- Real GPS detection (mobile)
- IP-based fallback (desktop)
- 10km home radius
- Permanent home location

### WorldEvents
Dynamic events:
- Meteor strikes
- Supply drops
- Resource surges
- World bosses

## Data Layer

### ScriptableObjects
- Items (base, weapon, armor, consumable)
- Building pieces
- Crafting recipes
- Game config

## Networking Architecture

### Authoritative Server Model
- Server validates all actions
- Client sends inputs/requests
- Server broadcasts state
- Lag compensation for combat

### Message Types
- Connection/spawn
- Position/state sync
- Combat actions
- Voice data
- World events

## Mobile Optimization

- Touch input with virtual joystick
- Reduced network update rate
- Chunk-based world loading
- Object pooling (not yet implemented)
- LOD system (not yet implemented)

## Next Steps

1. **Unity Setup**: Open in Unity, add Mirror package
2. **Prefab Creation**: Create player, item, building prefabs
3. **Scene Setup**: Create game scenes with proper hierarchy
4. **UI Implementation**: Build inventory, HUD, menus
5. **Art Integration**: Add anime-style sprites/models
6. **Testing**: Local multiplayer testing
7. **Server Deployment**: Set up authoritative server
