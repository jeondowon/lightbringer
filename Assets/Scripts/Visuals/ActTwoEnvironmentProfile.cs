using UnityEngine;

namespace Lightbringer.Visuals
{
    [CreateAssetMenu(menuName = "Lightbringer/Act Two Environment")]
    public sealed class ActTwoEnvironmentProfile : ScriptableObject
    {
        // Trees have unit height; landmarks have unit horizontal bounding-circle radius.
        public GameObject[] trees = new GameObject[0];
        public GameObject sanctuary;
        public GameObject monument;
        public GameObject watchtower;
        public GameObject brokenWall;
        public Material terrain;

        public GameObject FindLandmark(string kind)
        {
            switch (kind)
            {
                case "Sanctuary": return sanctuary;
                case "Monument": return monument;
                case "Watchtower": return watchtower;
                case "BrokenWall": return brokenWall;
                default: return null;
            }
        }
    }
}
