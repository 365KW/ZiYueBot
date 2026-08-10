# 统计 {#stat}

**统计 (Stat)** 是一个 [一般命令](/general/)，可以获取调用者在子悦机器上的数据简报。

数据统计包括当前平台及调用者账号信息、[赞助信息](/sponsors.md)、[漂流云瓶](/general/driftbottle/) 扔出数量及占比、近七天漂流云瓶增长数及用户占比、[俄罗斯轮盘](/harmony/revolver/) 、[黑名单信息](/technical/blacklists.md)、 [记过](/technical/management/penalty.md) 数据。

完整的统计数据另见：https://www.ziyuebot.cn/stat.html

## 用法 {#usage}

```
/stat
```

## 参数 {#params}

无

## 输出 {#output}

```
{用户名} 的统计数据
平台：{}
ID: {}
赞助到期时间：{}
您共扔出了 {} 支云瓶，占全部云瓶的 {}，总浏览量 {} 次。
您在俄罗斯轮盘命令中，向别人开过 {} 枪，其中打死过 {} 次。您向自己开过 {} 次枪，其中打死过 {} 次。总射击准度 {}%。
您被列入黑名单的命令有：{}
您共有 {} 条全局记过，详情请查看网页版统计数据。

完整版统计数据另见：https://www.ziyuebot.cn/stat.html
```

## 频率限制 {#rate-limit}

每次调用间隔 5 分钟；[赞助者](/sponsors.md) 1 分钟。
