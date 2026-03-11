using TylinkInspection.Core.Models;

namespace TylinkInspection.UI.ViewModels;

public sealed class ReviewEvidenceCardViewModel : ObservableObject
{
    private bool _isSelected;

    public ReviewEvidenceCardViewModel(ReviewEvidenceItem evidence)
    {
        Evidence = evidence;
    }

    public ReviewEvidenceItem Evidence { get; }

    public string DeviceName => Evidence.DeviceName;

    public string DeviceCode => Evidence.DeviceCode;

    public string RegionText => Evidence.RegionAndSchemeText;

    public string CapturedAtText => Evidence.CapturedAtText;

    public string SourceText => Evidence.EvidenceKindText;

    public string EvidenceRoleText => Evidence.EvidenceRoleText;

    public string PlaybackGradeText => Evidence.PlaybackGradeText;

    public string AbnormalTagText => Evidence.AbnormalTagText;

    public string ManualConclusionText => Evidence.ManualReviewConclusionText;

    public string AccentResourceKey => IsSelected ? "TonePrimaryBrush" : Evidence.AccentResourceKey;

    public string ImageUri => Evidence.ImageUri;

    public bool HasImageUri => Evidence.HasImageUri;

    public string DetailText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Evidence.AiAlertContent))
            {
                return Evidence.AiAlertContent;
            }

            if (!string.IsNullOrWhiteSpace(Evidence.FailureReason))
            {
                return Evidence.FailureReason;
            }

            return "可在右侧人工复核面板中写回复核结论，并联动直播/回看快速播放。";
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                RaisePropertyChanged(nameof(AccentResourceKey));
            }
        }
    }
}
