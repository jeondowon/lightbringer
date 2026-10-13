using UnityEngine;

namespace Lightbringer.Core
{
    // Battlefield-local routes. Enemy troops follow the same corridor in reverse.
    public static class CampaignStages
    {
        public const int Count = 24;
        // Stages 1-8 keep the original procedural layout; 9 onward are authored below.
        private const int FirstDefined = 9;
        private sealed class Definition
        {
            public readonly string Name, Brief;
            public readonly int Waves;
            public readonly Vector3[][] Routes;
            public Definition(string name, string brief, int waves, params Vector3[][] routes)
            { Name = name; Brief = brief; Waves = waves; Routes = routes; }
        }

        private static Vector3[] Route(float start, float lower, float upper, float gate) => new[] {
            new Vector3(start, 0, -5), new Vector3(lower, 0, 18), new Vector3(upper, 0, 43),
            new Vector3(gate, 0, 64), new Vector3(0, 0, BattlefieldLayout.EnemyBaseZ) };

        private static readonly Definition[] Defined = {
            new Definition("잿빛 순례길", "지붕을 잃은 성소와 부서진 돌길에 옛 용사의 흔적이 남아 있습니다. 강변의 두 순례길을 따라 봉화를 되찾으세요.", 6,
                Route(-11, -22, -15, -9), Route(11, 15, 23, 9)),
            new Definition("무너진 용사의 뜰", "부러진 용사 기념비와 성소의 뜰을 군세가 점령했습니다. 낡은 포장길에서 방패병으로 화살을 막고 전진하세요.", 6,
                Route(-12, -20, -7, -11), Route(12, 20, 7, 11)),
            new Definition("무너진 감시탑", "고사목과 폐허 사이로 옛 감시탑이 무너져 있습니다. 좁게 모이는 길에서 중장병과 주술사를 돌파하세요.", 7,
                Route(-10, -7, -7, -10), Route(10, 7, 7, 10)),
            new Definition("점령된 보급창", "옛 길목의 창고에 군주의 보급품이 쌓여 있습니다. 목책 거점을 잇는 세 길을 밀어 보급창을 탈환하세요.", 7,
                Route(-22, -25, -20, -12), Route(0, -3, 3, 0), Route(22, 25, 20, 12)),
            new Definition("잿빛 목재 야적장", "불탄 경작지 너머 군주의 목재 야적장이 남아 있습니다. 두 외곽 보급로의 군집병을 막고 거점을 끊으세요.", 8,
                Route(-11, -26, -24, -10), Route(11, 19, 26, 10)),
            new Definition("군용 마차 교차로", "마차 집결지와 목책 초소를 중심으로 세 길의 증원이 합류합니다. 전선을 나누어 투입하고 밀리는 곳을 지원하세요.", 8,
                Route(-22, -27, -18, -12), Route(0, 5, -3, 0), Route(22, 27, 18, 12)),
            new Definition("경비대의 돌다리", "차가운 강안개 속 돌다리를 군주의 경비대가 지킵니다. 세 교량을 돌파하면 얼음 마법을 배웁니다.", 8,
                Route(-22, -19, -25, -12), Route(0, 0, 0, 0), Route(22, 19, 25, 12)),
            new Definition("돌아온 군주의 성채", "공세와 성채를 무너뜨리고 군주와 다시 맞서세요. 병력을 지킨 채 검기 뒤의 돌진에 대비하세요.", 9,
                Route(-11, -21, -16, -10), Route(11, 21, 16, 10)),
            // Act 3 (CampaignAtlas_Act3): withered basin -> outer strongholds -> the final citadel. It opens a step easier.
            new Definition("시든 숲의 경계", "마지막 숲이 시들어 가는 마왕의 영역 어귀입니다. 버려진 마을 곁의 두 길로 첫 봉화를 되찾으세요.", 7,
                Route(-11, -18, -14, -9), Route(11, 17, 13, 9)),
            new Definition("검은 여울 다리", "차갑게 식은 강 위로 옛 돌다리 셋이 남아 있습니다. 다리를 건너는 세 길을 지켜 강 건너편 봉화를 밝히세요.", 8,
                Route(-22, -24, -20, -12), Route(0, -2, 2, 0), Route(22, 24, 20, 12)),
            new Definition("부서진 성소 언덕", "무너진 성소와 경계 벽이 언덕을 따라 흩어져 있습니다. 군주가 뒤에서 지휘하는 군세를 세 갈래에서 몰아내세요.", 8,
                Route(-21, -26, -22, -12), Route(0, 4, -4, 0), Route(21, 26, 22, 12)),
            new Definition("검은 고사목 골짜기", "검게 죽은 숲 사이로 어둠의 안개가 고여 있습니다. 골짜기를 메우는 군집병을 마법으로 흩어 놓으세요.", 9,
                Route(-20, -12, -24, -11), Route(0, -5, 5, 0), Route(20, 12, 24, 11)),
            new Definition("갈라진 옛 들판", "땅이 갈라진 옛 경작지에 마왕의 외곽 진지가 들어섰습니다. 균열 사이로 이어진 세 길을 밀어 진지를 무너뜨리세요.", 9,
                Route(-23, -18, -26, -12), Route(1, -4, 4, 0), Route(23, 18, 26, 12)),
            new Definition("잿빛 바위 협곡", "마왕의 성을 지탱하는 거점이 잿빛 바위 협곡을 막고 있습니다. 중장병이 지키는 길목을 창병으로 뚫으세요.", 9,
                Route(-22, -27, -19, -12), Route(0, 5, -5, 0), Route(22, 27, 19, 12)),
            new Definition("마지막 집결지", "마왕의 성으로 오르는 마지막 진입로입니다. 외곽 성문을 무너뜨리고 최후의 진격을 위한 집결지를 확보하세요.", 10,
                Route(-24, -20, -14, -10), Route(0, 0, 0, 0), Route(24, 20, 14, 10)),
            new Definition("두 어둠의 성채", "하늘을 뒤덮은 마왕과 어둠의 군주가 함께 기다립니다. 성채를 무너뜨린 뒤 땅에 내려온 어둠의 핵과 군주를 모두 쓰러뜨리세요.", 9,
                Route(-22, -26, -20, -12), Route(0, -3, 3, 0), Route(22, 26, 20, 12)),
        };

        private static bool IsDefined(int stage) => stage >= FirstDefined && stage <= Count;
        public static int ActOf(int stage) => Mathf.Clamp((stage - 1) / 8 + 1, 1, 3);
        public static string Name(int stage) => IsDefined(stage) ? Defined[stage - FirstDefined].Name : $"봉화 {stage:00}";
        public static string Brief(int stage) => IsDefined(stage) ? Defined[stage - FirstDefined].Brief
            : "전장을 돌파하고 봉화에 다시 빛을 밝혀주세요.";
        public static int PathCount(int stage) => IsDefined(stage) ? Defined[stage - FirstDefined].Routes.Length
            : stage <= 2 ? 1 : stage <= 5 ? 2 : 3;
        public static int WaveCount(int stage) => IsDefined(stage) ? Defined[stage - FirstDefined].Waves : 3 + stage / 2;
        public static Vector3[] RoutePoints(int stage, int lane)
        {
            if (IsDefined(stage)) return (Vector3[])Defined[stage - FirstDefined].Routes[lane].Clone();
            float x = (lane - (PathCount(stage) - 1) * .5f) * 22f;
            return new[] { new Vector3(x, 0, -5), new Vector3(x, 0, 34),
                new Vector3(x * .5f, 0, 64), new Vector3(0, 0, BattlefieldLayout.EnemyBaseZ) };
        }
        public static Vector3[] EnemyRoutePoints(int stage, int lane)
        {
            Vector3[] forward = RoutePoints(stage, lane), reverse = new Vector3[forward.Length];
            for (int i = 0; i < reverse.Length - 1; i++) reverse[i] = forward[forward.Length - 2 - i];
            reverse[reverse.Length - 1] = new Vector3(0, 0, -19.5f);
            return reverse;
        }
    }
}
