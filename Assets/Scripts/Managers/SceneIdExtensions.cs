using KingCardsSpire.Models;

namespace KingCardsSpire.Managers
{
    public static class SceneIdExtensions
    {
        public static string ToAddress(this SceneId sceneId)
        {
            return sceneId switch
            {
                SceneId.Main => "Scenes/Main",
                SceneId.Battle => "Scenes/Battle",
                _ => string.Empty
            };
        }
    }
}
