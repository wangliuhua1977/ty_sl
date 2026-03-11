# 天翼视联 AI 智能巡检系统 - Codex 压缩对接基线

> 用途：把当前已上传资料压缩成一份适合 Codex 桌面版阅读的基线文档。  
> 目标：只保留后续开发真正会反复用到的规则、接口、字段、流程和排障点，删除门户页眉、重复示例代码、长篇原理说明、下载链接、无关截图残留和敏感配置原文。  
> 适用项目：天翼视联 AI 智能巡检系统（.NET 8 Windows 桌面，本地运行）。

---

## 1. 本文档保留什么

只保留 8 类内容：

1. 项目冻结技术基线
2. 天翼视联公共请求规范
3. 用户无感知令牌策略
4. 目录/设备/状态接口
5. 普通告警与 AI 告警接口
6. 直播流与云回看接口
7. 地图与坐标基线
8. 高频排障要点

下列内容不再原样带入：

- 门户网页导航、Logo、重复页眉页脚
- 大段示例代码原文
- 冗长的算法工具类源码
- 已上传的真实凭证、密钥、Webhook、地图 Key 原文
- 仅用于门户操作说明、但对当前开发没有直接价值的长篇文字

---

## 2. 项目冻结基线

### 2.1 项目定位

当前项目本质上是一个部署在用户电脑上的 **.NET 8 Windows 桌面端运维巡检客户端**，核心面向“慢直播点位运维巡检”。

### 2.2 固定技术与结构

- 技术栈：`.NET 8`
- 运行形态：`Windows 桌面本地客户端`
- 工程分层：`Core / Infrastructure / Services / UI / App`
- 外部平台：`天翼视联能力开放平台`
- 地图：`高德地图`
- 地图统一展示坐标系：`GCJ-02`
- 通知链路：`企业微信机器人 Webhook`、`量子密信机器人 Webhook`
- 本地持久化：允许少量本地 JSON / 配置文件 / 运行态缓存

### 2.3 当前业务主线

后续代码组织默认围绕以下链路，不要偏成纯播放器或纯大屏：

1. 接口判定  
2. 点位体检  
3. 可播放性判断  
4. 截图 / 播放复核  
5. 故障派单  
6. 定时复检  
7. 恢复销警  
8. 报表沉淀

---

## 3. 天翼视联公共概念

- `AppId / AppSecret`：应用级接入凭证。
- `accessToken`：除登录授权类接口外，几乎所有能力接口都要带。
- `refreshToken`：用于刷新 `accessToken`。
- `enterpriseUser`：企业主手机号；**用户无感知获取令牌模式下是关键参数**。
- `parentUser`：当 `enterpriseUser` 是子账号时必传。
- `deviceCode`：设备唯一主标识，后续设备状态、拉流、告警、回看都围绕它。
- 目录体系：主要分 `业务树/监控目录树`、`设备树`、`行业树`。

---

## 4. 公共请求规范（必须统一封装）

### 4.1 通信与提交方式

- 协议：`HTTPS`
- 方法：`POST`
- 编码：`UTF-8`
- 提交格式：`application/x-www-form-urlencoded`
- 返回格式：`JSON`
- 正式环境域名：`https://vcp.21cn.com`
- 通用路径前缀：`https://vcp.21cn.com/open/token/`

### 4.2 公共表单参数

所有私有业务参数先拼成 `key=value&key=value...`，再整体加密为 `params`，然后统一提交：

- `appId`
- `clientType`
- `params`
- `timestamp`
- `version`
- `signature`

### 4.3 固定头参数

- Header 必传：`apiVersion: 2.0`

### 4.4 clientType 约定

当前桌面端固定按 **PC** 处理：

- `clientType = 3`

### 4.5 version 约定

平台文档中有两种口径：

- `v1.0`：常见于 XXTea 场景
- `1.1`：常见于 RSA 场景

当前项目实现时不要把“data 一律按某一种方式解密”写死，必须按接口返回规则和版本约定统一收敛在加解密服务里。

---

## 5. 加密与签名规则

### 5.1 参数加密

私有参数先拼接原文，再使用 **XXTea** 加密：

- 明文格式：`参数1=值&参数2=值&参数3=值`
- 密钥：`AppSecret` 转十六进制后参与 XXTea
- 文档说明：**私有请求参数不要求排序**

### 5.2 签名

签名算法固定为：

- `HMAC-SHA256`

签名参与字段为公共请求字段，文档强调顺序正确；示例代码中做法是：

1. 取公共请求参数集合
2. 按 key 忽略大小写排序
3. 将非空 value 顺序拼接
4. 以 `AppSecret` 做 HMAC-SHA256
5. 输出十六进制小写字符串

### 5.3 响应解密

上传资料显示平台存在两类返回：

- 直接返回普通 JSON 对象
- `data` 字段为加密串，需再解密

因此代码里必须有统一的：

- `请求参数加密器`
- `签名器`
- `响应解密器`
- `按 version/接口规则判断 data 解密方式`

不要在每个接口服务里各自复制一套加解密逻辑。

---

## 6. 令牌基线（用户无感知模式）

### 6.1 主模式

当前项目固定按：**用户无感知获取令牌**。

适用理解：已明确企业主账号，且项目方已获得相应授权，不走扫码登录流程。

### 6.2 获取 / 刷新接口

- 接口：`https://vcp.21cn.com/open/oauth/getAccessToken`
- `grantType = vcp_189`：首次获取令牌
- `grantType = refresh_token`：刷新令牌
- 刷新时必传：`refreshToken`

### 6.3 有效期

根据上传文档：

- `accessToken`：`7 天`
- `refreshToken`：`30 天`

### 6.4 正确策略

令牌不是一次一取的消耗品，必须按下列流程实现：

1. 首次获取后本地缓存
2. 接口调用前先检查 `accessToken` 是否仍有效
3. 有效则直接复用
4. 失效则检查 `refreshToken`
5. `refreshToken` 有效则刷新并替换本地缓存
6. `refreshToken` 也失效才重新申请新令牌

### 6.5 结合流程图后的落地结论

根据上传的“能开 API 调用流程图”，应实现为：

- 无令牌：先取令牌
- 有令牌：先检查 `expiresIn / expiresin`
- 令牌有效：直接调业务接口
- 令牌过期：检查刷新令牌有效期 `refreshExpiresIn / refreshExpiresin`
- 刷新令牌有效：刷新
- 刷新令牌失效：重新获取

### 6.6 需要重点避免的错误

- 每调一个接口就重新取 token
- UI 页面直接参与 token 刷新
- 令牌状态散落多个 ViewModel
- 忽略 `enterpriseUser` 与当前 token 绑定关系

建议做成单一 `TokenService + TokenCache`。

---

## 7. 目录与设备基础接口

### 7.1 监控目录树

#### 查询目录树
- 字段标识：`getReginWithGroupList`
- URL：`/open/token/device/getReginWithGroupList`
- 关键入参：`regionId`、`accessToken`、`enterpriseUser`、`parentUser`
- 关键说明：
  - `regionId` 为空时默认取首层目录
  - 返回中要关注：`hasChildren`、`havDevice`
  - 若要拉完整目录，需要按 `hasChildren` 递归
  - 普通用户调用可能报：`50004 获取业务树目录列表异常`

#### 查询当前目录设备列表
- 字段标识：`getDeviceList`
- URL：`/open/token/device/getDeviceList`
- 关键入参：`regionId`、`pageNo`、`pageSize`
- 关键说明：只查当前目录层级设备，不自动下钻

#### 分页查询账号下全部设备
- 字段标识：`getAllDeviceListNew`
- URL：`/open/token/device/getAllDeviceListNew`
- 关键入参：`cusRegionId`、`hasChildDevices`、`lastId`、`pageSize`
- 关键说明：
  - `lastId` 首次传 `0`
  - 后续翻页传上次返回的 `lastId`
  - 返回 `-1` 表示查完
  - `hasChildDevices=0` 时偏向返回可拉流设备码清单
  - `hasChildDevices=1` 时返回多目主设备和子设备层级

### 7.2 设备详情

#### 获取设备详细信息
- 字段标识：`showDevice`
- URL：`/open/token/device/showDevice`
- 核心入参：`deviceCode`
- 常用返回关注项：
  - `deviceCode`
  - `deviceName`
  - `deviceType`
  - `deviceModel`
  - `firmwareVersion`
  - `isCloudCamera`
  - 其他厂商 / 型号 / 国标相关字段

### 7.3 设备状态

#### 老接口：批量设备状态
- 字段标识：`batchDeviceStatus`
- URL：`/open/token/device/batchDeviceStatus`
- 入参：`deviceCodes`（最多 20 个，逗号分隔）、`queryData`
- `queryData`：
  - `1` 在线状态
  - `2` 云存状态
  - `3` 绑定状态
- 状态值：
  - 在线：`1 在线 / 0 离线 / -1 不在企业主名下 / 2 休眠 / 3 保活休眠`

#### 新接口：批量在线状态
- URL：`/open/token/vpaas/device/batchDeviceStatus`
- 入参：`deviceCodes`（单次最多 200 个）
- 状态值：`0 离线 / 1 在线 / 2 休眠 / 3 保活休眠`

#### 新接口：单设备在线状态
- 字段标识：`getDeviceStatus`
- URL：`/open/token/vpaas/device/getDeviceStatus`
- 入参：`deviceCode`
- 返回关注：`status`、`deviceCode`

### 7.4 设备侧开发建议

本项目建议：

- “设备清单拉取”和“设备在线状态刷新”拆开做
- 目录树用递归构建本地巡检范围
- 状态查询走批量接口优先
- 单设备详情只在点位详情页或复核时补查

---

## 8. 普通设备告警接口

### 8.1 告警列表

- 接口：`/open/token/device/getDeviceAlarmMessage`
- 核心入参：
  - `deviceCode`
  - `alertTypeList`
  - `pageNo`
  - `pageSize`
  - `alertSource`
  - `startTime`
  - `endTime`
- 时间格式：`yyyy-MM-dd HH:mm:ss:SSS`
- 文档中明确的类型示例：
  - `1` 设备离线
  - `10` 设备上线
  - `2` 画面变动 / 移动侦测
  - `11` 有人移动

### 8.2 返回使用注意

- 返回的每条告警主键是 `id`
- 前端滚动分页时，文档特别提示：**下一页需要通过上一页最后一条记录的时间和 id 做去重过滤**
- 适合当前项目作为：离线告警、恢复告警、基础异常态势统计的底层来源

---

## 9. AI 告警接口

### 9.1 AI 告警列表

- 接口：`/open/token/AIAlarm/getAlertInfoList`
- 关键入参：
  - `pageNo`
  - `pageSize`
  - `startTime`
  - `endTime`
  - `alertTypeList`
  - `deviceCode`
  - `alertSource`

### 9.2 文档中出现的 AI 类型

列表里出现的类型较多，当前项目先记住这些最相关的：

- `3` 画面异常巡检
- `4` 时光缩影
- `5` 区域入侵
- `6` 车牌布控
- `7` 人脸布控
- `12` 客流统计
- `13` 厨帽识别
- `14` 抽烟识别
- `15` 口罩识别
- `16` 玩手机识别
- `17` 火情识别
- `18` / `19` / `20` 平安慧眼相关
- `21` 大象识别
- `22` 电动车识别
- `23` / `24` 水域监控
- `25` 人群聚集检测
- `26` 医用防护服检测
- `27` 高空抛物

### 9.3 AI 告警详情

- 接口：`/open/token/AIAlarm/getAlertInfoDetail`
- 核心入参：
  - `msgId`
  - `alertType`
  - `deviceCode`
- 详情页设计必须兼容这三个字段的组合，不要只拿 `msgId` 作为唯一上下文

### 9.4 区域入侵抓拍图

- 接口：`/open/token/AIAlarm/getSnapImgUrl`
- 核心入参：`msgId`、`alertType`、`deviceCode`
- 文档明确：**只供区域入侵类告警使用**
- 抓拍图链接有效期：`1 天`

### 9.5 AI 图片刷新

- 接口：`/open/token/ai/task/source/refreshDownloadUrl`
- 作用：刷新已过期图片下载地址
- 结论：AI 图片、抓拍图、音频链接都不能假设永久有效

### 9.6 AI 告警在本项目中的使用建议

- `AI智能巡检中心` 的主异常来源，可优先聚焦：画面异常巡检、区域入侵、火情、客流、人脸/车牌布控
- 告警详情页至少保留：摘要信息、设备信息、抓拍图、产生时间、消息源、原始返回字段快照
- 图片下载地址要做过期重取能力

---

## 10. 直播流能力

### 10.1 直播流接口族

上传资料中出现的直播相关接口：

- `getDeviceMediaUrlHls` → `/open/token/cloud/getDeviceMediaUrlHls`
- `getDeviceMediaUrlRtmp` → `/open/token/cloud/getDeviceMediaUrlRtmp`
- `getDeviceMediaWebrtcUrl` → `/open/token/vpaas/getDeviceMediaWebrtcUrl`
- `getDeviceMediaUrlFlv` → `/open/token/cloud/getDeviceMediaUrlFlv`
- `getH5StreamUrl` → `/open/token/vpaas/getH5StreamUrl`

### 10.2 直播常用入参

通用核心一般包括：

- `deviceCode`
- `accessToken`
- `enterpriseUser`
- `parentUser`
- `mediaType`
- `mute`
- `netType`
- `expire`

其中文档明确：

- `mediaType`：高清 / 标清选择
- `mute`：静音标识
- `expire`：流地址有效期，最大 30 分钟，默认 5 分钟，建议非必要不设太长

### 10.3 H5 / 新播放器返回结构重点

文档中对新版返回有这些关键字段：

- `expireIn`
- `videoEnc`：`0-H.264`、`1-H.265`
- `streamUrls[]`
- `protocol`
- `streamUrl`
- `level`

### 10.4 流协议选择建议

文档综合后，可给 Codex 的实际结论：

- 桌面客户端做“可播放性判断”时，不要只绑定一种协议
- 优先做“按协议降级重试”机制：`WebRTC / HLS / FLV / RTMP` 视播放器能力选取
- 返回的是**临时流地址**，不可长期缓存为固定地址
- 需要识别 `H.264 / H.265`
- 播放失败时需区分：地址过期、设备离线、编码不支持、跨域/协议问题、网关问题

---

## 11. 云回看能力

### 11.1 目录与文件列表

- `getCloudFolderList` → `/open/token/cloud/getCloudFolderList`
- `getCloudFileList` → `/open/token/cloud/getCloudFileList`

`getCloudFileList` 常用参数：

- `deviceCode`
- `path`：按天查询，格式 `yyyyMMdd`
- `type`
  - `1` 按天查询
  - `2` 按时间段查询（`startDate` / `endDate` 必填，且只能查同一天）
- `startDate` / `endDate`：格式 `yyyyMMddHHmm`
- `orderBy`
- `pageNo`
- `pageSize`

### 11.2 回看文件下载地址

- `getFileUrlById` → `/open/token/cloud/getFileUrlById`
- 核心入参：`deviceCode`、`fileId`
- 文档提示：返回 `data` 可能为加密串，解密后拿到真实下载地址

### 11.3 回看流化地址

- `streamUrlRtmp` → `/open/token/cloud/streamUrlRtmp`
- `streamUrlHls` → `/open/token/cloud/streamUrlHls`

常用入参：

- `deviceCode`
- `fileId`
- `mute`
- `type`
  - `3` HLS(HTTP)
  - `4` HLS(HTTPS)

### 11.4 回看开发结论

- 告警复盘、人工复核、截图留痕都要依赖云回看
- 回看查询应支持：按天、按时间段、按页翻页
- 下载地址 / 流化地址都视为**临时地址**
- 代码里需要把“目录、文件列表、播放地址、下载地址”拆成四个清晰能力，不要混在一个服务方法里

---

## 12. 视频协议与播放器侧结论

上传资料给出的协议说明要点：

- `HLS`：m3u8 + TS 分片，通用性强
- `RTMP`：历史协议，兼容性受播放器和环境影响较大
- `WebRTC`：低时延，适合实时预览和巡检复核

结合排障手册，可得出当前项目的实用结论：

1. 流地址常常是临时地址，过期后必须重新取
2. 不同设备可能输出 `H.264` 或 `H.265`
3. 部分播放器不支持 H.265，需要兼容降级或替换解码能力
4. “有声音没画面”“打开黑屏”“VLC 不能播”都不能直接归因于接口错误，可能是编码、协议、网关或播放器问题
5. 可播放性判断最好做成：
   - 先取流
   - 再播放器试播
   - 再记录失败原因分类

---

## 13. 地图与坐标基线

### 13.1 地图体系

- 地图服务：`高德`
- 展示坐标系：`GCJ-02`

### 13.2 坐标规则

根据已上传项目基线文档，必须坚持：

1. 接口返回若是 `BD-09`，展示前先转 `GCJ-02`
2. 本地人工补录坐标按地图展示坐标直接使用
3. 地图最终展示统一以 `GCJ-02` 为准
4. 后续导入模板、手工补录页面、地图拾点交互，都要明确提示“按高德 GCJ-02 填写”

### 13.3 本地手工补录坐标

已冻结的本地规则：

- 唯一键：`deviceCode`
- 本地文件：`%LocalAppData%\...\device-manual-coordinates.json`
- 仅影响本地展示与本地统计
- 不回写平台
- 同一 `deviceCode` 多条记录时，最后一条生效

---

## 14. 高频排障要点（从排障手册压缩）

### 14.1 令牌与授权

常见问题集中在：

- 授权类型与应用类型不匹配
- `accessToken` 过期
- `refreshToken` 过期
- `accessToken` 为空
- `refreshToken` 为空
- `enterpriseUser` 不是合法云眼用户 / 企业主
- token 不是当前 `appId` 名下合法令牌

开发侧处理原则：

- 令牌获取失败先检查授权模式是否真的是“用户无感知”
- 再检查 `enterpriseUser` 是否已在门户完成授权
- 再检查是否误用了其他应用下的 token

### 14.2 门户与接口权限

高频问题：

- `30032`：应用无接口权限
- `30052`：商品未开通
- `30201`：接口不支持
- `30043`：应用不存在
- `20103` / `30108`：IP 白名单问题

开发侧处理原则：

- 某接口持续失败时，不要只查代码，先确认该能力是否已开通、应用是否有权限、IP 是否在白名单

### 14.3 目录与设备权限

高频问题：

- `50004`：获取业务树目录列表异常
- `20001`：用户无设备权限
- 设备目录为空、首层区域为空、查不到下级目录

开发侧处理原则：

- 先确认调用账号是否真的是企业主或有相应设备权限
- 目录树接口返回空时，先核对账号权限，不要直接判系统 bug

### 14.4 直播 / 回看常见问题

高频现象：

- 流地址过期
- 设备离线
- H.265 导致播放器无法解码
- RTMP / FLV 在某些播放器中不可播
- 跨域、跨协议、网关问题
- 云回看文件能查到，但下载地址拿不到或已失效

开发侧处理原则：

- 日志中单独记录：取流成功、试播成功、失败分类、失效时间
- 把“接口取流成功但播放器失败”与“接口本身失败”分开统计

### 14.5 AI 告警图片问题

高频现象：

- 图片链接过期
- 下载下来的图片打不开
- 查询不到历史 AI 消息

开发侧处理原则：

- 抓拍图、图片下载地址、音频地址全部视为短期资源
- 详情页需要“刷新下载地址”能力
- 历史消息查询要受时间范围、告警类型、设备权限影响

---

## 15. 对 Codex 最有用的落地约束

### 15.1 服务拆分建议

至少拆成以下服务边界：

- `TokenService`
- `OpenApiSigner / RequestEncryptor / ResponseDecryptor`
- `DirectoryService`
- `DeviceService`
- `LiveStreamService`
- `CloudReplayService`
- `AlarmService`
- `AiAlarmService`
- `MapCoordinateService`
- `NotificationService`

### 15.2 统一模型关键词

后续代码命名尽量围绕这些核心字段，不要各页面自己发明别名：

- `enterpriseUser`
- `parentUser`
- `accessToken`
- `refreshToken`
- `deviceCode`
- `regionId`
- `msgId`
- `alertType`
- `fileId`

### 15.3 当前最值得优先打通的接口链

建议按下面顺序落地：

1. 获取 / 刷新 token  
2. 查询目录树  
3. 查询设备列表 / 全量设备分页  
4. 查询批量在线状态  
5. 获取单设备详情  
6. 获取直播流并做试播判定  
7. 获取普通告警  
8. 获取 AI 告警列表 + 详情 + 抓拍图  
9. 获取云回看列表 + 播放地址

---

## 16. 本次压缩时明确剔除的原文件噪声

本文件已阅读并吸收但未原样保留的来源包括：

- `天翼视联-能力开放门户.md` 的重复门户导航与页面残留
- `名词解释.md`、`基本规范.md`、`签名请求加密.md`、`消息加密.md` 中重复内容
- `开发者ID_API.txt`、`高德地图API.txt` 中的真实凭证原文
- `天翼视联-能力开放对接常见问题排障手册.docx` 中大量门户操作型和非当前阶段必需内容
- 图片流程图已转写为第 6 章的令牌流程结论，不再单独带图

---

## 17. 给新线程的使用方式

如果要给 Codex 新线程喂背景，建议只放：

1. 本文件
2. 当前项目自己的 `AGENTS.md / skills`
3. 当前阶段要改的页面或模块清单

不要再把全部原始门户导出文档一次性塞进背景；需要某个接口细节时，再单独补充原始文档即可。

