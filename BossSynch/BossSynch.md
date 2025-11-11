# The following is the standard format of a serialized boss update packet

All data is sent as a byte array. The first byte determines the type of packet. Following data is determinds by the type of packet.

| Data Type | Data Length | Description |
| --------- | ----------- | ----------- |
| byte      | 1           | packet type |
| varies    | varies      | packet data |

### Packet Types

The following packet types are defined:

| Value | Packet Type                |
| ----- | -------------------------- |
| 0     | Boss Session Join Request  |
| 1     | Boss Session Join Response |
| 2     | Boss Session Update        |
| 3     | Boss Session Leave         |
| 4     | Boss Session End           |

## Boss Session Join Request

The boss session join request packet is sent to all other players when a boss session is requested. The packet contains the following data:

| Data Type | Data Length | Description |
| --------- | ----------- | ----------- |
| byte      | 1           | packet type |
| byte      | 1           | boss id     |
| bool      | 1           | is memory   |

### Boss ID

The boss id is a unique identifier for each boss. Currently, no bosses are supported so lol gottem. But here are the ids that each boss will have when they are implemented:

| Boss ID | Boss Name          |
| ------- | ------------------ |
| 0       | Runic Construct    |
| 1       | Gilded Serpent     |
| 2       | The Wizard         |
| 4       | Null               |
| 5       | The Spearman       |
| 6       | Wraith             |
| 7       | Boss Rush          |
| 8       | Wraith Prime       |
| 9       | Spirit of the Tomb |

### Is Memory

If true, signifies that the requested boss is the memory variant which is unique from the regular boss.

## Boss Session Join Response

The boss session join response packet is sent in response to a boss session join request packet indicating if the sender is currently hosting a cooresponding boss session or not. The packet structure is defined as follows:

| Data Type | Data Length | Description |
| --------- | ----------- | ----------- |
| byte      | 1           | packet type |
| bool      | 1           | is hosting  |

### Is Hosting

If true, the sender is hosting a boss session that matches the boss id in the boss session join request packet.

## Boss Session Update

The boss session update packet is sent to all other players in the current boss session if the sender is the host, otherwise it is sent exclusively to the host. The packet structure is unique to each boss and is defined in the respective boss's documentation.

## Boss Session Leave

The boss session leave packet is sent to the host when the sender is leaving the current boss session. If the host is the sender, the packet is sent to the next player in the current boss session's player list who will be the new host. If no applicable player is found, the boss session is terminated. The packet structure is defined as follows:

| Data Type | Data Length | Description |
| --------- | ----------- | ----------- |
| byte      | 1           | packet type |

## Boss Session End

The boss session end packet is sent by the host to all other players in the current boss session when the boss session is terminated (usually when the boss is defeated). The packet structure is defined as follows:

| Data Type | Data Length | Description |
| --------- | ----------- | ----------- |
| byte      | 1           | packet type |
