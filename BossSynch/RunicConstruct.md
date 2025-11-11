## The following is the format for Boss Session Update packets for the Runic Construct boss

This packet vas two variants, the first is used exclusively by the host and the second is used by all other players in the current boss session.

The packet structure for the host is defined as follows:

| Data Type | Data Length | Description     |
| --------- | ----------- | --------------- |
| byte      | 1           | packet type     |
| bool      | 1           | is host (true)  |
| float     | 4           | boss pos x      |
| float     | 4           | boss pos y      |
| float     | 4           | boss rotation   |
| float     | 4           | satlite a pos x |
| float     | 4           | satlite a pos y |
| float     | 4           | satlite b pos x |
| float     | 4           | satlite b pos y |
| float     | 4           | boss hp         |
| float     | 4           | boss max hp     |
| float     | 4           | boss defense    |
| float[]   | varies      | proj spawn x    |
| float[]   | varies      | proj spawn y    |
| float[]   | varies      | proj spawn rot  |

The packet structure for all other players is defined as follows:

| Data Type | Data Length | Description     |
| --------- | ----------- | --------------- |
| byte      | 1           | packet type     |
| bool      | 1           | is host (false) |
| float     | 4           | delta damage    |
