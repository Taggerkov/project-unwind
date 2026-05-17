using Systems.Stage;
using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "StageSelectionDataSO", menuName = "Unwind Database/Stage/Stage Selection Data")]
public class StageSelectionDataSO : ScriptableObject
{
    public string stageDisplayName;
    public Sprite stageThumbnail;
    
    public AssetReferenceT<StageEntrySO> stageEntryReference;
}
