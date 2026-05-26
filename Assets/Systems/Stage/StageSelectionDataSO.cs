using Systems.Stage;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// ScriptableObject that contains the display data shown in the stage-selection UI:
/// a human-readable name, a thumbnail sprite, and an Addressable reference to the
/// <see cref="Systems.Stage.StageEntrySO"/> that defines the playable scene.
/// Create via <c>Unwind Database → Stage → Stage Selection Data</c>.
/// </summary>
[CreateAssetMenu(fileName = "StageSelectionDataSO", menuName = "Unwind Database/Stage/Stage Selection Data")]
public class StageSelectionDataSO : ScriptableObject
{
    /// <summary>Human-readable name shown in the stage selection screen.</summary>
    public string stageDisplayName;

    /// <summary>Preview thumbnail displayed alongside the stage name in selection UI.</summary>
    public Sprite stageThumbnail;

    /// <summary>Addressable reference to the <see cref="Systems.Stage.StageEntrySO"/> loaded when this stage is chosen.</summary>
    public AssetReferenceT<StageEntrySO> stageEntryReference;
}
