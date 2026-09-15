# Unit and Building Unlock Tree

## Visual Tree

```text
Main Base
├─ Worker
├─ Production
│  ├─ Soldier
│  ├─ Ranger
│  ├─ Striker
│  ├─ Tank
│  ├─ Swarm
│  └─ Spliter Production → Spliter
├─ Vehicle Factory → Siege
├─ Maintenance Bay → Medic
└─ Signal Relay → Scout
```

## Buildings

| Building | Requirement | Produces / enables | Unit relationship |
|---|---|---|---|
| Main Base | Start | Worker, basic base expansion | Worker constructs all buildings |
| Production | Main Base | Standard combat roster | Soldier, Ranger, Striker, Tank, Swarm |
| Spliter Production | Production | Area melee line | Spliter |
| Vehicle Factory | Production | Heavy artillery line | Siege |
| Maintenance Bay | Production | Field support line | Medic |
| Signal Relay | Production | Recon line and extended vision | Scout |
| Auto Turret | Production | Static local defense | Supports Worker and ranged formations |
| Speed Aura | Production | Local movement support | Benefits assault and response units |

## Units

| Unit | Production building | Prerequisite | Role | Intended counterplay |
|---|---|---|---|---|
| Worker | Main Base | Start | Gather, return resources, construct | Protected by combat units |
| Soldier | Production | Start | Reliable front line | General-purpose baseline |
| Ranger | Production | Start | Long-range support | Vulnerable to fast melee pressure |
| Striker | Production | Start | Fast melee assault | Pressures slow artillery |
| Tank | Production | Start | Slow heavy ranged damage | Needs screening and positioning |
| Swarm | Production | Start | Low-cost surround pressure | Efficient when grouped |
| Spliter | Spliter Production | Production | Short-range multi-target brawler | Strong into clumps |
| Siege | Vehicle Factory | Production | Long-range anti-structure artillery | Slow and exposed while repositioning |
| Medic | Maintenance Bay | Production | Fast vision and support positioning | Non-combat; needs escort |
| Scout | Signal Relay | Production | Fast reconnaissance and light ranged pressure | Fragile; avoids front line |

## Dependency Matrix

| Unit / building | Main Base | Production | Spliter Prod. | Vehicle Factory | Maintenance Bay | Signal Relay |
|---|---:|---:|---:|---:|---:|---:|
| Worker | Produce | - | - | - | - | - |
| Soldier/Ranger/Striker/Tank/Swarm | - | Produce | - | - | - | - |
| Spliter | - | Required | Produce | - | - | - |
| Siege | - | Required | - | Produce | - | - |
| Medic | - | Required | - | - | Produce | - |
| Scout | - | Required | - | - | - | Produce |
