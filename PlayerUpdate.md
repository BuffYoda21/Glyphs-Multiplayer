# Player Update Packet Formating

All player update packets are sent as an array of bytes. The first 8 bytes form the sender's SteamID, the next byte is the packet type, and the rest of the bytes are the data for the packet.

| Data Type | Data Length | Description     |
| --------- | ----------- | --------------- |
| ulong     | 8           | sender steam id |
| bool      | 1           | is priority     |
| byte[]    | variable    | packet data     |

## Unreliable Packet

Used when sending more frequent or unimportant data to other players. Contains the following data:

| Data Type | Data Length | Description      |
| --------- | ----------- | ---------------- |
| ulong     | 8           | sender steam id  |
| bool      | 1           | priority (false) |
| float     | 4           | pos x            |
| float     | 4           | pos y            |
| byte      | 1           | hat id           |
| byte      | 1           | scene id         |
| int       | 4           | string len       |
| string    | variable    | user name        |

### Hat ID

The hat id is a unique identifier for each hat.

| Hat ID | Hat Name  |
| ------ | --------- |
| 0      | None      |
| 1      | Bow       |
| 2      | Propeller |
| 3      | Traffic   |
| 4      | John      |
| 5      | Top       |
| 6      | Fez       |
| 7      | Party     |
| 8      | Bomb      |
| 9      | Crown     |
| 10     | Chicken   |

### Scene ID

The scene id is a unique identifier for each playable scene.

| Scene ID | Scene Name     |
| -------- | -------------- |
| 0        | Title/Cutscene |
| 1        | Game           |
| 2        | Memory         |
| 3        | Outer Void     |

## Reliable Packet

Used when sending important data to other players. Contains the following data:

| Data Type | Data Length | Description     |
| --------- | ----------- | --------------- |
| ulong     | 8           | sender steam id |
| bool      | 1           | priority (true) |
| bool      | 1           | is attack       |
| float     | 4           | atk angle       |
| float     | 4           | atk bonus       |
| bool      | 1           | is dash attack  |

### Is Attack / Attack Angle / Attack Bonus

Is attack is true if the player is attacking. If true, the attack angle is the angle of the attack. The attack bonus is the additional damage that the player's attack will deal.

### Is Dash Attack

Is dash attack is true if the player is dash attacking.
