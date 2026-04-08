using UnityEditor;

namespace Underground.EditorTools
{
    public static class Phase1SceneBuilder
    {
        [MenuItem("Underground/Legacy/Phase 1/Create Vehicle Test Scene")]
        public static void CreateVehicleTestScene()
        {
            UndergroundPrototypeBuilder.BuildVehicleTestSceneOnly();
        }

        [MenuItem("Underground/Legacy/Phase 1/Rebuild Scene Prefabs")]
        public static void RebuildScenePrefabs()
        {
            UndergroundPrototypeBuilder.RebuildGeneratedScenePrefabs();
        }

        [MenuItem("Underground/Legacy/Phase 1/Build Full Prototype")]
        public static void BuildFullPrototype()
        {
            UndergroundPrototypeBuilder.BuildFullPrototype();
        }
    }
}
