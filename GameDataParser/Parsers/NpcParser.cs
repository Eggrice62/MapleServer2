﻿using System.Xml;
using GameDataParser.Files;
using GameDataParser.Parsers.Helpers;
using GameDataParser.Tools;
using Maple2.File.IO.Crypto.Common;
using Maple2Storage.Enums;
using Maple2Storage.Types;
using Maple2Storage.Types.Metadata;

namespace GameDataParser.Parsers;

public class NpcParser : Exporter<List<NpcMetadata>>
{
    public NpcParser(MetadataResources resources) : base(resources, MetadataName.Npc) { }

    protected override List<NpcMetadata> Parse()
    {
        // Parse EXP tables
        Dictionary<int, ExpMetadata> levelExp = new();
        foreach (PackFileEntry entry in Resources.XmlReader.Files)
        {
            if (!entry.Name.StartsWith("table/expbasetable"))
            {
                continue;
            }

            XmlDocument document = Resources.XmlReader.GetXmlDocument(entry);
            foreach (XmlNode node in document.DocumentElement.ChildNodes)
            {
                if (node.Name != "table")
                {
                    continue;
                }

                if (int.Parse(node.Attributes["expTableID"].Value) != 1)
                {
                    continue;
                }

                foreach (XmlNode tableNode in node.ChildNodes)
                {
                    if (tableNode.Name != "base")
                    {
                        continue;
                    }

                    ExpMetadata expTable = new();

                    byte level = byte.Parse(tableNode.Attributes["level"].Value);
                    if (level == 0)
                    {
                        continue;
                    }

                    expTable.Level = level;
                    expTable.Experience = long.Parse(tableNode.Attributes["exp"].Value);
                    levelExp[level] = expTable;
                }
            }
        }

        // Parse the NpcId -> Names first.
        Dictionary<int, string> npcIdToName = new();
        foreach (PackFileEntry entry in Resources.XmlReader.Files.Where(entry => entry.Name.Equals("string/en/npcname.xml")))
        {
            XmlDocument document = Resources.XmlReader.GetXmlDocument(entry);

            foreach (XmlNode node in document.SelectNodes("ms2/key"))
            {
                int id = int.Parse(node.Attributes["id"].Value);
                if (!npcIdToName.ContainsKey(id))
                {
                    npcIdToName.Add(id, node.Attributes["name"].Value);
                }
            }
        }

        // Handle /npc files second, to setup the NpcMetadata
        List<NpcMetadata> npcs = new();
        foreach (PackFileEntry entry in Resources.XmlReader.Files.Where(entry => entry.Name.StartsWith("npc/")))
        {
            XmlDocument document = Resources.XmlReader.GetXmlDocument(entry);

            XmlNode npcModelNode = document.SelectSingleNode("ms2/environment/model") ?? document.SelectSingleNode("ms2/model");
            XmlNode npcBasicNode = document.SelectSingleNode("ms2/environment/basic") ?? document.SelectSingleNode("ms2/basic");
            XmlNode npcStatsNode = document.SelectSingleNode("ms2/environment/stat") ?? document.SelectSingleNode("ms2/stat");
            XmlNode npcSpeedNode = document.SelectSingleNode("ms2/environment/speed") ?? document.SelectSingleNode("ms2/speed");
            XmlNode npcDistanceNode = document.SelectSingleNode("ms2/environment/distance") ?? document.SelectSingleNode("ms2/distance");
            XmlNode npcSkillNode = document.SelectSingleNode("ms2/environment/skill") ?? document.SelectSingleNode("ms2/skill");
            XmlNode npcEffectNode = document.SelectSingleNode("ms2/environment/additionalEffect") ?? document.SelectSingleNode("ms2/additionalEffect");
            XmlNode npcExpNode = document.SelectSingleNode("ms2/environment/exp") ?? document.SelectSingleNode("ms2/exp");
            XmlNode npcAiInfoNode = document.SelectSingleNode("ms2/environment/aiInfo") ?? document.SelectSingleNode("ms2/aiInfo");
            XmlNode npcNormalNode = document.SelectSingleNode("ms2/environment/normal") ?? document.SelectSingleNode("ms2/normal");
            XmlNode npcDeadNode = document.SelectSingleNode("ms2/environment/dead") ?? document.SelectSingleNode("ms2/dead");
            XmlNode npcDropItemNode = document.SelectSingleNode("ms2/environment/dropiteminfo") ?? document.SelectSingleNode("ms2/dropiteminfo");
            XmlNode npcCapsuleNode = document.SelectSingleNode("ms2/environment/capsule") ?? document.SelectSingleNode("ms2/capsule");
            XmlAttributeCollection statsCollection = npcStatsNode.Attributes;

            // Metadata
            NpcMetadata metadata = new()
            {
                Id = int.Parse(Path.GetFileNameWithoutExtension(entry.Name))
            };
            metadata.Name = npcIdToName.ContainsKey(metadata.Id) ? npcIdToName[metadata.Id] : "";
            metadata.NpcMetadataModel.Model = npcModelNode.Attributes["kfm"].Value;
            float.TryParse(npcModelNode.Attributes["scale"].Value, out metadata.NpcMetadataModel.Scale);

            // Parse basic attribs.
            metadata.TemplateId = int.TryParse(npcBasicNode.Attributes["illust"]?.Value, out _) ? int.Parse(npcBasicNode.Attributes["illust"].Value) : 0;
            metadata.Type = (NpcType) byte.Parse(npcBasicNode.Attributes["friendly"].Value);
            metadata.Level = byte.Parse(npcBasicNode.Attributes["level"].Value);

            metadata.NpcMetadataBasic.NpcAttackGroup = sbyte.Parse(npcBasicNode.Attributes["npcAttackGroup"]?.Value ?? "0");
            metadata.NpcMetadataBasic.NpcDefenseGroup = sbyte.Parse(npcBasicNode.Attributes["npcDefenseGroup"]?.Value ?? "0");
            metadata.NpcMetadataBasic.Difficulty = ushort.Parse(npcBasicNode.Attributes["difficulty"]?.Value ?? "0");
            metadata.NpcMetadataBasic.MaxSpawnCount = byte.Parse(npcBasicNode.Attributes["maxSpawnCount"]?.Value ?? "0");

            metadata.NpcMetadataBasic.GroupSpawnCount = byte.Parse(npcBasicNode.Attributes["groupSpawnCount"]?.Value ?? "0");
            metadata.NpcMetadataBasic.MainTags = npcBasicNode.Attributes["mainTags"]?.Value.Split(",").Select(p => p.Trim()).ToArray() ?? Array.Empty<string>();
            metadata.NpcMetadataBasic.SubTags = npcBasicNode.Attributes["subTags"]?.Value.Split(",").Select(p => p.Trim()).ToArray() ?? Array.Empty<string>();
            metadata.NpcMetadataBasic.Class = byte.Parse(npcBasicNode.Attributes["class"].Value);
            metadata.NpcMetadataBasic.Kind = (NpcKind) ushort.Parse(npcBasicNode.Attributes["kind"].Value);
            metadata.NpcMetadataBasic.HpBar = byte.Parse(npcBasicNode.Attributes["hpBar"].Value);
            metadata.NpcMetadataBasic.MinimapIconName = npcBasicNode.Attributes["minimapIconName"]?.Value ?? "";

            metadata.NpcStats = StatParser.ParseStats(statsCollection);

            // Parse speed
            metadata.NpcMetadataSpeed.RotationSpeed = float.Parse(npcSpeedNode.Attributes["rotation"]?.Value ?? "0");
            metadata.NpcMetadataSpeed.WalkSpeed = float.Parse(npcSpeedNode.Attributes["walk"]?.Value ?? "0");
            metadata.NpcMetadataSpeed.RunSpeed = float.Parse(npcSpeedNode.Attributes["run"]?.Value ?? "0");

            // Parse distance
            metadata.NpcMetadataDistance.Avoid = int.Parse(npcDistanceNode.Attributes["avoid"]?.Value ?? "0");
            metadata.NpcMetadataDistance.Sight = int.Parse(npcDistanceNode.Attributes["sight"]?.Value ?? "0");
            metadata.NpcMetadataDistance.SightHeightUp = int.Parse(npcDistanceNode.Attributes["sightHeightUP"]?.Value ?? "0");
            metadata.NpcMetadataDistance.SightHeightDown = int.Parse(npcDistanceNode.Attributes["sightHeightDown"]?.Value ?? "0");

            // Parse skill
            metadata.NpcMetadataSkill.SkillIds = npcSkillNode.Attributes["ids"].Value.SplitAndParseToInt(',').ToArray();
            if (metadata.NpcMetadataSkill.SkillIds.Length > 0)
            {
                metadata.NpcMetadataSkill.SkillLevels = npcSkillNode.Attributes["levels"]?.Value.SplitAndParseToByte(',').ToArray();
                metadata.NpcMetadataSkill.SkillPriorities = npcSkillNode.Attributes["priorities"]?.Value.SplitAndParseToByte(',').ToArray();
                metadata.NpcMetadataSkill.SkillProbs = npcSkillNode.Attributes["probs"]?.Value.SplitAndParseToShort(',').ToArray();
                metadata.NpcMetadataSkill.SkillCooldown = short.Parse(npcSkillNode.Attributes["coolDown"].Value);
            }

            // Parse Additional Effects (Effect / Buff)
            metadata.NpcMetadataEffect.EffectIds = npcEffectNode.Attributes["codes"].Value.SplitAndParseToInt(',').ToArray();
            if (metadata.NpcMetadataEffect.EffectIds.Length > 0)
            {
                metadata.NpcMetadataEffect.EffectLevels = npcEffectNode.Attributes["levels"]?.Value.SplitAndParseToByte(',').ToArray();
            }

            // Parse normal state
            List<(string, NpcAction, short)> normalActions = new();
            string[] normalActionIds = npcNormalNode.Attributes["action"]?.Value.Split(",") ?? Array.Empty<string>();
            if (normalActionIds.Length > 0)
            {
                short[] actionProbs = npcNormalNode.Attributes["prob"]?.Value.SplitAndParseToShort(',').ToArray();
                for (int i = 0; i < normalActionIds.Length; i++)
                {
                    normalActions.Add((normalActionIds[i], GetNpcAction(normalActionIds[i]), actionProbs[i]));
                }

                metadata.StateActions[NpcState.Normal] = normalActions.ToArray();
            }

            metadata.MoveRange = short.Parse(npcNormalNode.Attributes["movearea"]?.Value ?? "0");

            // HACK: Parse combat/skills state (does not actually exist)
            List<(string, NpcAction, short)> combatActions = new();
            string[] combatActionsIds =
            {
                "Run_A"
            };
            if (combatActionsIds.Length > 0)
            {
                int equalProb = 10000 / combatActionsIds.Length;
                int remainder = 10000 % (equalProb * combatActionsIds.Length);
                combatActions.Add((combatActionsIds[0], GetNpcAction(combatActionsIds[0]), (short) (equalProb + remainder)));
                metadata.StateActions[NpcState.Combat] = combatActions.ToArray();
            }

            // Parse dead state
            List<(string, NpcAction, short)> deadActions = new();
            string[] deadActionIds = npcDeadNode.Attributes["defaultaction"]?.Value.Split(",") ?? Array.Empty<string>();
            if (deadActionIds.Length > 0)
            {
                int equalProb = 10000 / deadActionIds.Length;
                int remainder = 10000 % (equalProb * deadActionIds.Length);
                deadActions.Add((deadActionIds[0], GetNpcAction(deadActionIds[0]), (short) (equalProb + remainder)));
                for (int i = 1; i < deadActionIds.Length; i++)
                {
                    deadActions.Add((deadActionIds[i], GetNpcAction(deadActionIds[i]), (short) equalProb));
                }

                metadata.StateActions[NpcState.Dead] = deadActions.ToArray();
            }

            metadata.AiInfo = npcAiInfoNode.Attributes["path"].Value;
            int customExpValue = int.Parse(npcExpNode.Attributes["customExp"].Value);
            metadata.Experience = customExpValue >= 0 ? customExpValue : (int) levelExp[metadata.Level].Experience;
            metadata.NpcMetadataDead.Time = float.Parse(npcDeadNode.Attributes["time"].Value);
            metadata.NpcMetadataDead.Actions = npcDeadNode.Attributes["defaultaction"].Value.Split(",");
            metadata.GlobalDropBoxIds = npcDropItemNode.Attributes["globalDropBoxId"].Value.SplitAndParseToInt(',').ToArray();
            metadata.ShopId = int.Parse(npcBasicNode.Attributes["shopId"].Value);

            // Parse capsule
            int radius = int.Parse(npcCapsuleNode.Attributes["radius"].Value);
            int height = int.Parse(npcCapsuleNode.Attributes["height"].Value);
            bool ignore = npcCapsuleNode.Attributes["ignore"].Value == "1";
            metadata.NpcMetadataCapsule = new(radius, height, ignore);

            npcs.Add(metadata);
        }

        return npcs;
    }

    private static NpcAction GetNpcAction(string name)
    {
        string actionName = name.Split('_')[0];
        return actionName switch
        {
            "Idle" => NpcAction.Idle,
            "Bore" => NpcAction.Bore,
            "Walk" => NpcAction.Walk,
            "Run" => NpcAction.Run,
            "Dead" => NpcAction.Dead,
            _ => NpcAction.None
        };
    }
}
