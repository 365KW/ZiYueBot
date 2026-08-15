# 云瓶审核 {#censorship}

**云瓶审核 (Censorship)** 是用于维护云瓶生态质量的外部机制。该部分的具体实现由不属于子悦机器的第三方平台提供。

[扔云瓶](/general/driftbottle/throw.md) 会将所有提交的云瓶写入 `driftbottles_queue` 数据库表，并由外部审核将可被通过的云瓶写入 `driftbottles` 数据库表。