using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Services;

public sealed class MockInspectionWorkspaceService : IInspectionWorkspaceService
{
    public InspectionWorkspaceData GetWorkspaceData()
    {
        return new InspectionWorkspaceData
        {
            OverviewMetrics =
            [
                new OverviewMetric { Label = "今日巡检总数", Value = "1,286", Unit = "次", DeltaText = "+12.8%", AccentResourceKey = "TonePrimaryBrush" },
                new OverviewMetric { Label = "在线覆盖", Value = "96.4", Unit = "%", DeltaText = "+1.6%", AccentResourceKey = "ToneSuccessBrush" },
                new OverviewMetric { Label = "发现异常", Value = "23", Unit = "项", DeltaText = "+5", AccentResourceKey = "ToneWarningBrush" },
                new OverviewMetric { Label = "待派单", Value = "11", Unit = "单", DeltaText = "2个高优", AccentResourceKey = "ToneInfoBrush" },
                new OverviewMetric { Label = "超时数", Value = "4", Unit = "项", DeltaText = "-1", AccentResourceKey = "ToneDangerBrush" }
            ],
            CurrentTask = new CurrentTaskStatus
            {
                Title = "华东区域日常视频点位巡检",
                RegionName = "区域: 上海 / 苏州 / 杭州",
                TimeWindow = "执行窗口: 08:30 - 18:00",
                CompletionRate = 0.72,
                CompletedCount = 186,
                AbnormalCount = 17,
                PendingReviewCount = 9
            },
            AlertItems =
            [
                new AlertItem { SiteName = "虹桥枢纽 01", Message = "视频流中断超过 180 秒", TimeText = "11:06", SeverityLabel = "高优先级", AccentResourceKey = "ToneDangerBrush" },
                new AlertItem { SiteName = "苏州园区 17", Message = "AI 识别到镜头遮挡", TimeText = "10:52", SeverityLabel = "中优先级", AccentResourceKey = "ToneWarningBrush" },
                new AlertItem { SiteName = "杭州西站 08", Message = "点位待复核超过 SLA", TimeText = "10:15", SeverityLabel = "待复核", AccentResourceKey = "ToneInfoBrush" },
                new AlertItem { SiteName = "嘉兴综合体 04", Message = "重点区域连续三次告警", TimeText = "09:41", SeverityLabel = "重点关注", AccentResourceKey = "ToneSuccessBrush" }
            ],
            MapMarkers =
            [
                new MapMarker { Name = "上海虹桥", StateLabel = "正常点位", X = 292, Y = 168, AccentBrushKey = "ToneSuccessBrush", GlowBrushKey = "MarkerNormalGlowBrush" },
                new MapMarker { Name = "苏州工业园", StateLabel = "异常点位", X = 438, Y = 188, AccentBrushKey = "ToneWarningBrush", GlowBrushKey = "MarkerAbnormalGlowBrush" },
                new MapMarker { Name = "杭州西站", StateLabel = "待复核点位", X = 520, Y = 278, AccentBrushKey = "ToneInfoBrush", GlowBrushKey = "MarkerReviewGlowBrush" },
                new MapMarker { Name = "宁波港区", StateLabel = "重点关注点位", X = 648, Y = 304, AccentBrushKey = "ToneFocusBrush", GlowBrushKey = "MarkerFocusGlowBrush" },
                new MapMarker { Name = "合肥站南广场", StateLabel = "正常点位", X = 208, Y = 270, AccentBrushKey = "ToneSuccessBrush", GlowBrushKey = "MarkerNormalGlowBrush" },
                new MapMarker { Name = "温州站", StateLabel = "异常点位", X = 618, Y = 392, AccentBrushKey = "ToneWarningBrush", GlowBrushKey = "MarkerAbnormalGlowBrush" }
            ],
            AssistantStatus = new AssistantStatus
            {
                Headline = "正在扫描 AI 异常态，请稍候...",
                Detail = "已完成当前地图区域 72% 视频流抽帧分析，正在叠加历史异常模式与点位健康评分。",
                LastAction = "最新动作: 已锁定 4 个需优先派单点位",
                ConfidenceText = "AI 置信区间 92.7%"
            },
            ProgressItems =
            [
                new ProgressItem { Label = "扫描中", Value = "18", AccentResourceKey = "TonePrimaryBrush" },
                new ProgressItem { Label = "已扫描", Value = "186", AccentResourceKey = "ToneSuccessBrush" },
                new ProgressItem { Label = "发现异常", Value = "23", AccentResourceKey = "ToneWarningBrush" }
            ],
            TodoItems =
            [
                new TodoItem { Title = "派发高优工单", Description = "虹桥枢纽 01 需 15 分钟内确认现场视频链路。", DueText = "截止 11:20", AccentResourceKey = "ToneDangerBrush" },
                new TodoItem { Title = "复核待办清单", Description = "杭州西站 08 与苏州园区 17 进入人工复核队列。", DueText = "截止 12:00", AccentResourceKey = "ToneInfoBrush" },
                new TodoItem { Title = "导出晨检简报", Description = "生成 08:00-12:00 巡检摘要，用于领导驾驶舱同步。", DueText = "截止 12:30", AccentResourceKey = "ToneSuccessBrush" }
            ],
            RadarSignals =
            [
                new RadarSignal { X = 140, Y = 78, Size = 10, Opacity = 0.95, AccentResourceKey = "ToneSuccessBrush" },
                new RadarSignal { X = 88, Y = 120, Size = 8, Opacity = 0.75, AccentResourceKey = "ToneInfoBrush" },
                new RadarSignal { X = 170, Y = 150, Size = 12, Opacity = 0.9, AccentResourceKey = "ToneWarningBrush" },
                new RadarSignal { X = 120, Y = 54, Size = 6, Opacity = 0.6, AccentResourceKey = "ToneFocusBrush" }
            ],
            AiInspectionCenterPage = BuildModulePage(
                "AI智能巡检中心",
                "统一承载 AI 巡检任务、批量作业与状态流转，当前为本地假数据演示。",
                "AI INSPECT",
                "TonePrimaryBrush",
                [Metric("待执行", "18", "项", "6个优先", "TonePrimaryBrush"), Metric("执行中", "7", "项", "2个重试", "ToneSuccessBrush"), Metric("已完成", "124", "项", "完成率 94%", "ToneInfoBrush"), Metric("异常中", "5", "项", "需介入", "ToneDangerBrush")],
                [Highlight("巡检批次调度", "华东晨检批次", "当前批次覆盖 186 个点位，执行链路与重试策略已预热完成。", "TonePrimaryBrush"), Highlight("异常作业池", "5 个智能巡检任务待处理", "以接口超时与视频流拉取失败为主，后续可接实际重试日志。", "ToneWarningBrush")],
                [Feed("AI-INS-20260310-0810", "常规巡检批次已完成 72%，预计 11:40 结束。", "11:08", "ToneSuccessBrush"), Feed("AI-INS-20260310-0830", "AI 异常态补扫任务进入重试通道。", "10:54", "ToneWarningBrush"), Feed("AI-INS-20260310-0900", "跨区域点位同步作业完成 96%。", "10:22", "ToneInfoBrush"), Feed("AI-INS-20260310-0915", "高优链路恢复后已自动重排队列。", "09:47", "TonePrimaryBrush")]
            ),
            ReviewCenterPage = BuildModulePage(
                "巡检复核中心",
                "面向人工复核、结果回写与驳回闭环的占位页，后续接入真实复核工作台。",
                "REVIEW FLOW",
                "ToneInfoBrush",
                [Metric("待复核", "9", "项", "3项超时", "ToneInfoBrush"), Metric("已复核", "58", "项", "今日累计", "ToneSuccessBrush"), Metric("已驳回", "4", "项", "需重检", "ToneWarningBrush"), Metric("争议中", "2", "项", "待升级", "ToneDangerBrush")],
                [Highlight("人工复核队列", "杭州西站 08", "检测到遮挡告警与离线告警冲突，当前需人工确认真实状态。", "ToneInfoBrush"), Highlight("复核规则快照", "双人复核策略生效", "高优告警需两级确认后才能关闭，当前仅做壳层展示。", "ToneFocusBrush")],
                [Feed("复核记录 #A103", "苏州园区 17 已由值班员完成确认并回写。", "11:02", "ToneSuccessBrush"), Feed("复核记录 #A101", "虹桥枢纽 01 被驳回，要求重新拉取视频片段。", "10:31", "ToneDangerBrush"), Feed("复核记录 #A098", "杭州西站 08 进入升级审核。", "10:08", "ToneInfoBrush"), Feed("复核记录 #A091", "重点关注点位完成闭环。", "09:36", "TonePrimaryBrush")]
            ),
            AiAlertCenterPage = BuildModulePage(
                "AI告警中心",
                "聚合 AI 异常识别结果、人工确认、派单与恢复状态，当前仅使用本地模拟数据。",
                "AI ALERTS",
                "ToneWarningBrush",
                [Metric("AI告警总数", "37", "条", "较昨日 +6", "ToneWarningBrush"), Metric("待确认", "11", "条", "需人工确认", "ToneInfoBrush"), Metric("已派单", "8", "条", "4条高优", "TonePrimaryBrush"), Metric("已恢复", "14", "条", "恢复率 87%", "ToneSuccessBrush")],
                [Highlight("异常聚类视图", "镜头遮挡占比 38%", "主要集中在交通枢纽与园区边缘点位，后续可接 AI 模型输出。", "ToneWarningBrush"), Highlight("派单建议", "4 个点位建议立即派单", "AI 助手综合置信度、历史趋势和 SLA 进行排序。", "ToneDangerBrush")],
                [Feed("AI-ALT-2031", "虹桥枢纽识别到持续黑屏，告警等级提升。", "11:06", "ToneDangerBrush"), Feed("AI-ALT-2024", "苏州园区遮挡告警待人工确认。", "10:52", "ToneWarningBrush"), Feed("AI-ALT-2017", "杭州西站异常已自动归并至工单。", "10:13", "TonePrimaryBrush"), Feed("AI-ALT-2008", "宁波港区模型置信度提升至 94%。", "09:48", "ToneSuccessBrush")]
            ),
            PointGovernancePage = BuildModulePage(
                "点位治理中心",
                "用于管理点位在线状态、定位质量和补录治理任务，方便后续接真实地图与台账。",
                "POINT OPS",
                "ToneSuccessBrush",
                [Metric("点位总数", "1,268", "个", "覆盖 23 区域", "TonePrimaryBrush"), Metric("在线", "1,182", "个", "在线率 93.2%", "ToneSuccessBrush"), Metric("异常", "39", "个", "待治理", "ToneWarningBrush"), Metric("未定位", "12", "个", "需补录", "ToneDangerBrush")],
                [Highlight("坐标补录任务", "12 个点位待补录", "当前优先处理跨区域迁移与新上线设备，避免影响地图巡检视图。", "ToneDangerBrush"), Highlight("治理策略概览", "在线点位每日自动巡检", "治理策略将决定后续地图层级与告警聚合方式。", "ToneSuccessBrush")],
                [Feed("点位治理单 #P032", "闵行园区新增 3 个点位待补坐标。", "11:01", "ToneDangerBrush"), Feed("点位治理单 #P027", "苏州园区 17 完成台账核对。", "10:29", "ToneSuccessBrush"), Feed("点位治理单 #P021", "杭州西站 08 进入异常治理池。", "10:02", "ToneWarningBrush"), Feed("点位治理单 #P018", "华东交通枢纽专题图层已刷新。", "09:33", "TonePrimaryBrush")]
            ),
            StrategyConfigPage = BuildModulePage(
                "策略配置中心",
                "承载巡检策略、AI 规则、自动派单与人工复核策略的配置入口，当前展示主题化占位结构。",
                "POLICY HUB",
                "ToneFocusBrush",
                [Metric("巡检策略", "16", "套", "3套启用", "TonePrimaryBrush"), Metric("AI规则", "24", "条", "2条待发布", "ToneFocusBrush"), Metric("自动派单", "8", "条", "命中率 89%", "ToneSuccessBrush"), Metric("人工复核", "6", "条", "1条调优中", "ToneWarningBrush")],
                [Highlight("巡检编排", "区域 + 时段双维度", "可按区域、时段和优先级组合生成巡检任务，后续接正式策略编辑器。", "TonePrimaryBrush"), Highlight("规则回滚预案", "保留最近 5 次快照", "策略发布后可快速回滚，避免影响在线巡检链路。", "ToneFocusBrush")],
                [Feed("策略变更 #S014", "AI 告警聚合阈值已提升至 3 次。", "11:04", "ToneFocusBrush"), Feed("策略变更 #S010", "自动派单规则新增高优告警直派。", "10:38", "ToneSuccessBrush"), Feed("策略变更 #S006", "人工复核超时阈值调整为 20 分钟。", "09:58", "ToneWarningBrush"), Feed("策略变更 #S003", "巡检窗口模板完成保存。", "09:21", "TonePrimaryBrush")]
            ),
            ReportCenterPage = BuildModulePage(
                "报表中心",
                "预留日报、周报、月报、趋势分析与导出能力，后续可无缝替换成真实报表模块。",
                "REPORTS",
                "TonePrimaryBrush",
                [Metric("日报", "12", "份", "今日已生成", "TonePrimaryBrush"), Metric("周报", "4", "份", "本周累计", "ToneInfoBrush"), Metric("月报", "1", "份", "待审阅", "ToneSuccessBrush"), Metric("异常趋势", "7", "类", "持续跟踪", "ToneWarningBrush")],
                [Highlight("巡检完成率", "96.4%", "常规巡检完成率持续稳定，后续可接图表控件替换占位内容。", "ToneSuccessBrush"), Highlight("异常趋势简报", "黑屏与离线为主", "趋势摘要适合在领导展示版主题中进一步强化表现。", "ToneWarningBrush")],
                [Feed("日报 2026-03-10", "晨检摘要已生成，可用于驾驶舱同步。", "11:05", "TonePrimaryBrush"), Feed("周报 2026-W11", "本周异常趋势摘要已刷新。", "10:41", "ToneInfoBrush"), Feed("月报 2026-03", "月报模板进入待审阅状态。", "10:06", "ToneSuccessBrush"), Feed("趋势任务 #R008", "异常完成率曲线已缓存。", "09:28", "ToneWarningBrush")]
            ),
            SystemSettingsPage = BuildModulePage(
                "系统设置",
                "集中展示接口、通知、地图、缓存与版本状态配置，当前只做安全的本地占位，不写真实密钥。",
                "SETTINGS",
                "ToneInfoBrush",
                [Metric("接口配置", "3", "组", "全部 Mock", "ToneInfoBrush"), Metric("通知配置", "2", "组", "企业内网", "ToneSuccessBrush"), Metric("地图配置", "1", "组", "占位模式", "TonePrimaryBrush"), Metric("版本状态", "v0.1", "", "UI Shell", "ToneFocusBrush")],
                [Highlight("敏感配置隔离", "未写入任何真实密钥", "后续接正式配置中心时，仍应走本地安全存储与环境变量机制。", "ToneDangerBrush"), Highlight("主题扩展入口", "已兼容 ThemeService", "新增标准控制台版时，无需重写当前 8 个页面。", "ToneInfoBrush")],
                [Feed("系统检查", "当前主题为科技态势版。", "11:09", "TonePrimaryBrush"), Feed("缓存状态", "本地假数据缓存正常。", "10:44", "ToneSuccessBrush"), Feed("地图状态", "仍处于 Mock Placeholder 模式。", "10:12", "ToneInfoBrush"), Feed("接口状态", "未接入真实天翼视联接口。", "09:39", "ToneDangerBrush")]
            )
        };
    }

    private static ModulePageData BuildModulePage(
        string title,
        string subtitle,
        string badgeText,
        string badgeAccentResourceKey,
        IReadOnlyList<OverviewMetric> summaryCards,
        IReadOnlyList<HighlightCard> highlightCards,
        IReadOnlyList<ActivityFeedItem> activityItems)
    {
        return new ModulePageData
        {
            PageTitle = title,
            PageSubtitle = subtitle,
            StatusBadgeText = badgeText,
            StatusBadgeAccentResourceKey = badgeAccentResourceKey,
            SummaryCards = summaryCards,
            HighlightCards = highlightCards,
            ActivityItems = activityItems
        };
    }

    private static OverviewMetric Metric(string label, string value, string unit, string deltaText, string accentResourceKey)
    {
        return new OverviewMetric
        {
            Label = label,
            Value = value,
            Unit = unit,
            DeltaText = deltaText,
            AccentResourceKey = accentResourceKey
        };
    }

    private static HighlightCard Highlight(string title, string headline, string description, string accentResourceKey)
    {
        return new HighlightCard
        {
            Title = title,
            Headline = headline,
            Description = description,
            AccentResourceKey = accentResourceKey
        };
    }

    private static ActivityFeedItem Feed(string title, string description, string metaText, string accentResourceKey)
    {
        return new ActivityFeedItem
        {
            Title = title,
            Description = description,
            MetaText = metaText,
            AccentResourceKey = accentResourceKey
        };
    }
}
