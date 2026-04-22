using UnityEngine;

[CreateAssetMenu(fileName = "RockWallDefinition", menuName = "Shoot the ROCK/Rock Wall Definition")]
public class RockWallDefinition : ScriptableObject
{
    [System.Serializable]
    public struct RevealStage
    {
        [Min(1f)] public float worldWidth;
        [Min(1f)] public float worldHeight;
        [Min(1f)] public float cellsPerUnit;
        [Range(0f, 1f)] public float revealThreshold;
        public Vector2 cameraPadding;
        public Vector2 cameraLookOffset;

        public RevealStage(float worldWidth, float worldHeight, float cellsPerUnit, float revealThreshold, Vector2 cameraPadding, Vector2 cameraLookOffset)
        {
            this.worldWidth = worldWidth;
            this.worldHeight = worldHeight;
            this.cellsPerUnit = cellsPerUnit;
            this.revealThreshold = revealThreshold;
            this.cameraPadding = cameraPadding;
            this.cameraLookOffset = cameraLookOffset;
        }
    }

    [SerializeField] private RevealStage[] revealStages =
    {
        new RevealStage(48f, 56f, 10.869565f, 0.20f, new Vector2(3.4f, 1.35f), new Vector2(0f, 0f)),
        new RevealStage(64f, 72f, 10.869565f, 0.22f, new Vector2(4.8f, 1.9f), new Vector2(1.2f, 0.6f)),
        new RevealStage(80f, 88f, 10.869565f, 1f, new Vector2(6.4f, 2.8f), new Vector2(2.4f, 1.2f)),
    };

    public RevealStage[] RevealStages => revealStages;

    public bool HasStages => revealStages != null && revealStages.Length > 0;

    public RevealStage GetStageOrLast(int index)
    {
        if (!HasStages)
            return CreateDefaultStages()[0];

        index = Mathf.Clamp(index, 0, revealStages.Length - 1);
        return revealStages[index];
    }

    public void ResetToDefaults()
    {
        revealStages = CreateDefaultStages();
    }

    public void SetStages(RevealStage[] stages)
    {
        if (stages == null || stages.Length == 0)
        {
            revealStages = CreateDefaultStages();
            return;
        }

        revealStages = (RevealStage[])stages.Clone();
    }

    public static RevealStage[] CreateDefaultStages()
    {
        return new[]
        {
            new RevealStage(48f, 56f, 10.869565f, 0.20f, new Vector2(3.4f, 1.35f), new Vector2(0f, 0f)),
            new RevealStage(64f, 72f, 10.869565f, 0.22f, new Vector2(4.8f, 1.9f), new Vector2(1.2f, 0.6f)),
            new RevealStage(80f, 88f, 10.869565f, 1f, new Vector2(6.4f, 2.8f), new Vector2(2.4f, 1.2f)),
        };
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (revealStages == null || revealStages.Length == 0)
        {
            revealStages = CreateDefaultStages();
            return;
        }

        for (int i = 0; i < revealStages.Length; i++)
        {
            RevealStage stage = revealStages[i];
            stage.worldWidth = Mathf.Max(1f, stage.worldWidth);
            stage.worldHeight = Mathf.Max(1f, stage.worldHeight);
            stage.cellsPerUnit = Mathf.Max(1f, stage.cellsPerUnit);
            stage.revealThreshold = Mathf.Clamp01(stage.revealThreshold);
            stage.cameraPadding.x = Mathf.Max(0f, stage.cameraPadding.x);
            stage.cameraPadding.y = Mathf.Max(0f, stage.cameraPadding.y);
            revealStages[i] = stage;
        }
    }
#endif
}
