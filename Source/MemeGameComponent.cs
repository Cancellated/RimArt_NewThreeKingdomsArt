using Verse;

namespace NewThreeKingdomsArt
{
    /// <summary>
    /// 游戏启动/读档后自动应用权重。
    /// 
    /// RimWorld 的 Game.FillComponents() 通过反射自动发现所有 GameComponent 子类并实例化，
    /// StartedNewGame() 和 LoadedGame() 会在 Def 加载完成后被调用，
    /// 此时可以安全地修改 RulePackDef。
    /// </summary>
    public class MemeGameComponent : GameComponent
    {
        public MemeGameComponent(Game game) { }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            MemeWeightApplier.Apply();
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            MemeWeightApplier.Apply();
        }
    }
}
