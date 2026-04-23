#if UNITY_EDITOR
using FullThrottle.SacredCore.Campaign;
using FullThrottle.SacredCore.Story;
using UnityEditor;
using UnityEngine;

namespace FullThrottle.SacredCore.EditorTools
{
    public static class FTCampaignAssetGenerator
    {
        private const string Root = "Assets/ScriptableObjects/FullThrottle";
        private const string CampaignFolder = Root + "/Campaign";
        private const string NarrativeFolder = Root + "/Narrative";
        private const string StoryActsFolder = Root + "/StoryActs";

        [MenuItem("Full Throttle/Sacred Core/Generate Gla1ve Cinematic Campaign Assets")]
        public static void GenerateCampaignAssets()
        {
            EnsureFolders();

            ChapterSeed[] seeds = BuildSeeds();
            FTStoryActDefinition[] acts =
            {
                CreateOrLoad<FTStoryActDefinition>(StoryActsFolder, "act_00_prologue"),
                CreateOrLoad<FTStoryActDefinition>(StoryActsFolder, "act_01_unknown_name"),
                CreateOrLoad<FTStoryActDefinition>(StoryActsFolder, "act_02_pressure_climb"),
                CreateOrLoad<FTStoryActDefinition>(StoryActsFolder, "act_03_roads_darker"),
                CreateOrLoad<FTStoryActDefinition>(StoryActsFolder, "act_04_crown_night"),
                CreateOrLoad<FTStoryActDefinition>(StoryActsFolder, "act_05_epilogue")
            };

            ConfigureAct(acts[0], "act_00_prologue", "Prologue", 0, "Marc Badua enters the city with almost nothing. The city has no reason to care yet.");
            ConfigureAct(acts[1], "act_01_unknown_name", "Unknown Name", 0, "Gla1ve survives City Core and forces King Serrano to recognize him.");
            ConfigureAct(acts[2], "act_02_pressure_climb", "Pressure Of The Climb", 1200, "The roads get cleaner, colder, and more expensive.");
            ConfigureAct(acts[3], "act_03_roads_darker", "The Roads Get Darker", 3000, "The city watches, the garage becomes vulnerable, and Thomas tests obsession.");
            ConfigureAct(acts[4], "act_04_crown_night", "Crown Of The Night", 5200, "The upper city, the highway, and Ray become the final wall.");
            ConfigureAct(acts[5], "act_05_epilogue", "Impossible To Erase", 7000, "The hierarchy changes. Marc remains. Gla1ve becomes permanent.");

            FTCampaignDefinition campaign = CreateOrLoad<FTCampaignDefinition>(CampaignFolder, "campaign_full_throttle_gla1ve");
            campaign.campaignId = "full_throttle_gla1ve";
            campaign.title = "Full Throttle";
            campaign.protagonistRealName = "Marc Badua";
            campaign.protagonistStreetName = "Gla1ve";
            campaign.thesis = "A nobody becomes a name that changes what the city respects.";
            campaign.chapters.Clear();

            FTCampaignChapterDefinition previous = null;
            for (int i = 0; i < seeds.Length; i++)
            {
                ChapterSeed seed = seeds[i];
                FTNarrativeBeatDefinition intro = CreateBeat(seed, "intro", FTNarrativeBeatType.IntroCinematic, seed.Intro, seed.LineSpeaker, seed.Line);
                FTNarrativeBeatDefinition post = CreateBeat(seed, "post", FTNarrativeBeatType.PostRace, seed.Post, "", "");
                FTNarrativeBeatDefinition garage = CreateBeat(seed, "garage", FTNarrativeBeatType.GarageScene, seed.Garage, "", "");
                FTNarrativeBeatDefinition radio = CreateBeat(seed, "radio", FTNarrativeBeatType.Radio, "", "City Radio", seed.Radio);

                FTCampaignChapterDefinition chapter = CreateOrLoad<FTCampaignChapterDefinition>(CampaignFolder, seed.Id);
                chapter.chapterId = seed.Id;
                chapter.actId = seed.ActId;
                chapter.chapterNumber = seed.Number;
                chapter.title = seed.Title;
                chapter.districtId = seed.District;
                chapter.narrativePurpose = seed.Purpose;
                chapter.requiredReputation = seed.RequiredRep;
                chapter.requiredChapterIds.Clear();
                if (previous != null)
                {
                    chapter.requiredChapterIds.Add(previous.chapterId);
                }

                chapter.requiredRaceWins.Clear();
                chapter.requiredRivalWins.Clear();
                if (!string.IsNullOrWhiteSpace(seed.RequiredRivalWin))
                {
                    chapter.requiredRivalWins.Add(seed.RequiredRivalWin);
                }

                chapter.introBeat = intro;
                chapter.postRaceBeat = post;
                chapter.garageBeat = garage;
                chapter.radioFollowUpBeat = radio;
                chapter.moneyReward = seed.MoneyReward;
                chapter.reputationReward = seed.RepReward;
                chapter.unlockDistrictIds.Clear();
                chapter.unlockRewardIds.Clear();
                chapter.unlockChapterIds.Clear();
                if (!string.IsNullOrWhiteSpace(seed.UnlockDistrict))
                {
                    chapter.unlockDistrictIds.Add(seed.UnlockDistrict);
                }

                if (i + 1 < seeds.Length)
                {
                    chapter.unlockChapterIds.Add(seeds[i + 1].Id);
                }

                EditorUtility.SetDirty(chapter);
                campaign.chapters.Add(chapter);
                previous = chapter;
            }

            EditorUtility.SetDirty(campaign);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = campaign;
            Debug.Log($"[SacredCore] Generated Gla1ve cinematic campaign assets. chapters={campaign.chapters.Count}, root={CampaignFolder}.");
        }

        private static FTNarrativeBeatDefinition CreateBeat(ChapterSeed seed, string suffix, FTNarrativeBeatType type, string direction, string speaker, string line)
        {
            string assetName = $"{seed.Id}_{suffix}";
            FTNarrativeBeatDefinition beat = CreateOrLoad<FTNarrativeBeatDefinition>(NarrativeFolder, assetName);
            beat.beatId = assetName;
            beat.chapterId = seed.Id;
            beat.title = seed.Title;
            beat.beatType = type;
            beat.blocksGameplay = type == FTNarrativeBeatType.IntroCinematic
                || type == FTNarrativeBeatType.GarageScene
                || type == FTNarrativeBeatType.DistrictTakeover;
            beat.cinematicDirection = direction;
            beat.radioCopy = type == FTNarrativeBeatType.Radio ? line : "";
            beat.dialogue.Clear();
            if (!string.IsNullOrWhiteSpace(line) && type != FTNarrativeBeatType.Radio)
            {
                beat.dialogue.Add(new FTDialogueLine
                {
                    characterId = ResolveCharacterId(speaker),
                    displayName = speaker,
                    line = line,
                    delayAfter = 0.65f
                });
            }

            EditorUtility.SetDirty(beat);
            return beat;
        }

        private static void ConfigureAct(FTStoryActDefinition act, string id, string title, int requiredRep, string summary)
        {
            act.actId = id;
            act.title = title;
            act.requiredReputation = requiredRep;
            act.summary = summary;
            act.introMonologue = summary;
            EditorUtility.SetDirty(act);
        }

        private static string ResolveCharacterId(string speaker)
        {
            if (string.IsNullOrWhiteSpace(speaker)) return "";
            return speaker.ToLowerInvariant().Replace(" ", "_").Replace("“", "").Replace("”", "").Replace("\"", "");
        }

        private static T CreateOrLoad<T>(string folder, string assetName) where T : ScriptableObject
        {
            string path = $"{folder}/{assetName}.asset";
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolders()
        {
            FTSacredCoreSetupWizard.EnsureFolder("Assets", "ScriptableObjects");
            FTSacredCoreSetupWizard.EnsureFolder("Assets/ScriptableObjects", "FullThrottle");
            FTSacredCoreSetupWizard.EnsureFolder(Root, "Campaign");
            FTSacredCoreSetupWizard.EnsureFolder(Root, "Narrative");
            FTSacredCoreSetupWizard.EnsureFolder(Root, "StoryActs");
        }

        private static ChapterSeed[] BuildSeeds()
        {
            return new[]
            {
                new ChapterSeed(0, "chapter_00_name_before_city", "act_00_prologue", "The Name Before The City", "city_core", 0, "Introduce Marc Badua, Ross, the city tone, and Lockup 13.", "Night rain, weak headlights, wet asphalt. Marc enters the city in a tired machine.", "Ross", "You sure this is the city you want?", "JM checks the starter car and calls it rough, but alive.", "A new plate rolled into the city tonight. Nobody knows if it survives.", "Garage unlocked.", "", 400, 100),
                new ChapterSeed(1, "chapter_01_filler_on_the_grid", "act_01_unknown_name", "Filler On The Grid", "city_core", 0, "Make the player feel small, broke, and underestimated.", "A low-tier lineup barely notices Marc. His car looks outclassed.", "Marc", "Talk all you want. I'm still here.", "Ross and JM push the idea that survival matters more than image.", "City Core has a new filler. Filler just won.", "Background chatter changes after the first wins.", "", 700, 180),
                new ChapterSeed(2, "chapter_02_city_notices", "act_01_unknown_name", "The City Notices", "city_core", 250, "Begin conflict with King Serrano.", "King watches clips of Marc's win and laughs like it means nothing.", "King Serrano", "These streets don't hand out names. They bury them.", "Ross explains City Core's hierarchy.", "King's runners are asking about the quiet new driver.", "King's first direct message lands.", "", 900, 220),
                new ChapterSeed(3, "chapter_03_lockup_13", "act_01_unknown_name", "Lockup 13", "city_core", 500, "Make the garage the emotional center and deepen the starter car bond.", "Quiet garage after a hard night. Tools, damaged panels, low money.", "JM Ormido", "It doesn't need to be pretty. It needs to survive tonight.", "First real garage identity scene.", "Lockup 13 is small, but people are starting to look toward it.", "Level 1 upgrades and garage audio preview open.", "", 500, 180),
                new ChapterSeed(4, "chapter_04_first_blood", "act_01_unknown_name", "First Blood", "city_core", 750, "Marc humiliates one of King's trusted runners.", "Crowded street start. King's runner calls Marc a tourist.", "Ross", "Let the road answer.", "The starter car is repaired like a weapon, not a trophy.", "A trusted runner got dropped tonight. King is not laughing now.", "Wager races unlock.", "", 1100, 300),
                new ChapterSeed(5, "chapter_05_king_of_the_core", "act_01_unknown_name", "King Of The Core", "city_core", 1200, "End Act I with the first district boss race.", "King arrives like City Core belongs to him. The crowd parts.", "King Serrano", "You wanted a name? Come take the weight that comes with it.", "Lockup 13 prepares the car like this race decides the shop's future.", "City Core changed hands tonight.", "City Core status won. Arterial access opens.", "arterial_zone", 1800, 500, "rival_king_serrano"),
                new ChapterSeed(6, "chapter_06_better_roads_worse_people", "act_02_pressure_climb", "Better Roads, Worse People", "arterial_zone", 1500, "Introduce Arterial Zone and bigger money.", "Wider roads, cleaner events, colder people.", "Ross", "Upper roads aren't friendlier. They're just better lit.", "JM talks about building for speed without losing the car's soul.", "New money is moving through Arterial tonight.", "Edd mission chain opens.", "", 1300, 320),
                new ChapterSeed(7, "chapter_07_edds_offer", "act_02_pressure_climb", "Edd's Offer", "arterial_zone", 1900, "Tempt Marc with dangerous advancement.", "Edd offers access to a bigger event with a price hidden under it.", "Edd Ricapor", "There's money in the city tonight. Question is how much of yourself you're willing to spend getting it.", "Ross distrusts the offer. Marc takes it anyway.", "Gla1ve bought access. Access always sends a bill.", "Higher stakes economy opens.", "", 1500, 360),
                new ChapterSeed(8, "chapter_08_mav_cruz", "act_02_pressure_climb", "Mav Cruz", "arterial_zone", 2300, "Introduce status, polish, and image as a rival force.", "Mav arrives like attention is part of his car's aero.", "Mav Cruz", "You drive like you've got nothing to lose. That only works until you actually do.", "JM frames presentation as pressure, not vanity.", "Mav Cruz noticed the new name. That's not the same as respect.", "Arterial ladder opens.", "", 1500, 380),
                new ChapterSeed(9, "chapter_09_collapse", "act_02_pressure_climb", "Collapse", "arterial_zone", 2600, "Marc suffers a costly downfall.", "A bad deal, bad timing, and too much pressure break against Marc.", "Marc", "How much?", "The garage is quiet. Damage speaks for everyone.", "The climb bent tonight. It did not break.", "Recovery arc opens.", "", 0, 0),
                new ChapterSeed(10, "chapter_10_back_from_concrete", "act_02_pressure_climb", "Back From The Concrete", "arterial_zone", 2600, "Rebuild Marc through grind and discipline.", "Tools, cheap parts, tired eyes. James Louis enters without pity.", "James Louis", "Winning's easy to talk about when you haven't paid for it yet.", "JM and Ross rebuild the car around discipline.", "Gla1ve is back running small events. That is not retreat. That is repair.", "Access restored.", "", 1600, 400),
                new ChapterSeed(11, "chapter_11_gold_can_bleed", "act_02_pressure_climb", "Gold Can Bleed", "arterial_zone", 3200, "Marc defeats Mav and breaks the image wall.", "Prestige event. Mav tries to make Marc look temporary.", "Marc", "I don't need your room. I need the road.", "The garage sees a car that finally looks like Marc's rise.", "Gold can bleed. Arterial saw it.", "Mountain access opens.", "mountain_fringe", 2300, 650, "rival_mav_cruz"),
                new ChapterSeed(12, "chapter_12_everybodys_watching", "act_03_roads_darker", "Everybody's Watching", "mountain_fringe", 3500, "Show Marc's rise becoming city-wide.", "Radio chatter, clips, boards, whispers. Gla1ve is now a city problem.", "Ross", "You don't need the city to love you. You need it to stop overlooking you.", "JM warns that attention breaks weak builds.", "The whole city is watching now.", "Heat rises harder.", "", 1400, 420),
                new ChapterSeed(13, "chapter_13_thomas_cabanit", "act_03_roads_darker", "Thomas Cabanit", "mountain_fringe", 3900, "Introduce Marc's deepest mirror.", "Fog, elevation, silence. Thomas appears without performance.", "Thomas Cabanit", "The road doesn't care who you are. That's why I trust it more than people.", "The garage prepares for roads that punish ego.", "Mountain people are quiet about Thomas. That says enough.", "Mountain trials open.", "", 1700, 460),
                new ChapterSeed(14, "chapter_14_home_is_not_safe", "act_03_roads_darker", "Home Is Not Safe", "mountain_fringe", 4300, "Threaten Lockup 13 and raise the stakes beyond racing.", "Lockup 13 feels vulnerable for the first time.", "Ross", "They didn't come for the car. They came for your peace.", "The garage scene is protective and restrained.", "Somebody touched Lockup 13. The city felt that mistake.", "Thomas boss chain opens.", "", 1000, 440),
                new ChapterSeed(15, "chapter_15_edge_of_the_mountain", "act_03_roads_darker", "Edge Of The Mountain", "mountain_fringe", 4800, "Marc faces Thomas without losing himself to obsession.", "Night mountain run. No crowd needed. The road is enough.", "Thomas Cabanit", "Don't chase the edge unless you know what you are willing to leave there.", "The car is quiet before the hardest technical run.", "Thomas gave respect. Mountain Fringe gave way.", "Highway Belt opens.", "highway_belt", 2600, 760, "rival_thomas_cabanit"),
                new ChapterSeed(16, "chapter_16_lance_dc", "act_04_crown_night", "Lance D.C.", "highway_belt", 5200, "Make speed terrifying.", "Late highway. Violent speed. Lance does not speak softly.", "Lance D.C.", "If you blink at this speed, you don't lose. You disappear.", "JM builds for stability because speed punishes lies.", "Highway Belt is awake. Lance wants blood.", "Upper-tier speed content opens.", "", 2200, 600),
                new ChapterSeed(17, "chapter_17_upper_world", "act_04_crown_night", "The Upper World", "highway_belt", 5700, "Introduce Ray and the city power structure.", "Cleaner, colder, controlled. Ray is not just another racer.", "Ray", "Don't mistake attention for power.", "James warns Marc about winning without becoming owned.", "Ray finally spoke. The city heard the leash in his voice.", "Final chain proximity.", "", 2400, 680),
                new ChapterSeed(18, "chapter_18_last_night_lockup_13", "act_04_crown_night", "Last Night In Lockup 13", "city_core", 6200, "Final sacred garage scene.", "Marc, Ross, JM, James. No speech. The car says enough.", "Marc", "Finish it.", "The final build locks in. The garage becomes a chapel for speed.", "Lockup 13 is quiet tonight. That means something is coming.", "Final readiness window.", "", 0, 500),
                new ChapterSeed(19, "chapter_19_crown_of_the_night", "act_04_crown_night", "Crown Of The Night", "highway_belt", 6800, "Final city-spanning showdown.", "The whole city watches a route that crosses every scar Marc earned.", "Ray", "The city remembers winners. It obeys owners.", "The garage has nothing left to add. Only the road remains.", "Marc Badua is the man. Gla1ve is the name the city will remember.", "Campaign complete. Legend-tier events unlock.", "", 5000, 1200, "rival_ray"),
                new ChapterSeed(20, "chapter_20_impossible_to_erase", "act_05_epilogue", "Impossible To Erase", "city_core", 7000, "Show the city after Marc's rise.", "The city is still dangerous. The hierarchy changed anyway.", "James Louis", "Permanent doesn't mean safe. It means they can't erase you.", "Lockup 13 is no longer a shelter. It is a landmark.", "The night did not end. It learned a new name.", "Postgame state opens.", "", 0, 0)
            };
        }

        private readonly struct ChapterSeed
        {
            public readonly int Number;
            public readonly string Id;
            public readonly string ActId;
            public readonly string Title;
            public readonly string District;
            public readonly int RequiredRep;
            public readonly string Purpose;
            public readonly string Intro;
            public readonly string LineSpeaker;
            public readonly string Line;
            public readonly string Garage;
            public readonly string Radio;
            public readonly string Post;
            public readonly string UnlockDistrict;
            public readonly int MoneyReward;
            public readonly int RepReward;
            public readonly string RequiredRivalWin;

            public ChapterSeed(int number, string id, string actId, string title, string district, int requiredRep, string purpose, string intro, string lineSpeaker, string line, string garage, string radio, string post, string unlockDistrict, int moneyReward, int repReward, string requiredRivalWin = "")
            {
                Number = number;
                Id = id;
                ActId = actId;
                Title = title;
                District = district;
                RequiredRep = requiredRep;
                Purpose = purpose;
                Intro = intro;
                LineSpeaker = lineSpeaker;
                Line = line;
                Garage = garage;
                Radio = radio;
                Post = post;
                UnlockDistrict = unlockDistrict;
                MoneyReward = moneyReward;
                RepReward = repReward;
                RequiredRivalWin = requiredRivalWin;
            }
        }
    }
}
#endif
