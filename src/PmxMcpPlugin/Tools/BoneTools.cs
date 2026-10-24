using System.Collections.Generic;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxMcp
{
    /// <summary>
    /// Bones, including the flags PMX calls display and operation limits.
    ///
    /// Two of those, the fixed axis and the local frame, do not change how the model
    /// deforms - the PMX specification is explicit about it. They constrain how the bone
    /// is handled in an editor. They are still worth reading and writing, because tools
    /// that offer manual posing need them.
    ///
    /// The local frame vectors are not on the IPXBone interface; see <see cref="PmxRef"/>.
    /// </summary>
    internal static class BoneTools
    {
        public static void Register(ToolRegistry registry, Editor editor)
        {
            registry.Add(
                "list_bones",
                "列出骨骼及其索引、名称、父骨骼和位置。分页返回，可按名称筛选。",
                Schema.Object(Json.Obj(
                    "offset", Schema.Int("返回的起始骨骼索引（默认 0）"),
                    "limit", Schema.Int("返回的骨骼数量（默认 200，最大 1000）"),
                    "name_contains", Schema.Str("仅返回日文名或英文名包含此文本的骨骼"))),
                true,
                delegate(Dictionary<string, object> args) { return ListBones(editor, args); });

            registry.Add(
                "get_bone",
                "查看单个骨骼的完整信息：标志位、变形层级、追加父骨骼、固定轴、局部坐标系轴和 IK 链接。"
                    + "可用索引或名称指定。",
                Schema.Object(Json.Obj(
                    "index", Schema.Int("骨骼索引"),
                    "name", Schema.Str("日文骨骼名"))),
                true,
                delegate(Dictionary<string, object> args) { return GetBone(editor, args); });

            registry.Add(
                "set_bone",
                "编辑单个骨骼。可用索引或名称指定，只修改传入的字段。"
                    + "骨骼引用可传索引（-1 表示无）或对应的 _name 字段。"
                    + "fix_axis 和 local_frame 只影响编辑器中的操作方式，不影响模型变形。",
                Schema.Object(Json.Obj(
                    "index", Schema.Int("骨骼索引"),
                    "name", Schema.Str("用于查找骨骼的日文名"),
                    "new_name", Schema.Str("新的日文名"),
                    "new_name_en", Schema.Str("新的英文名"),
                    "position", Schema.NumArray("新的骨骼位置 [x, y, z]", 3),
                    "parent", Schema.Int("父骨骼索引，-1 表示无"),
                    "parent_name", Schema.Str("父骨骼名称"),
                    "to_bone", Schema.Int("尾端骨骼索引，将该骨骼设为尾端；-1 表示无"),
                    "to_bone_name", Schema.Str("尾端骨骼名称"),
                    "to_offset", Schema.NumArray("以偏移量表示尾端 [x, y, z]；会清除尾端骨骼", 3),
                    "level", Schema.Int("变形层级（变换顺序）"),
                    "visible", Schema.Bool("在编辑器中显示该骨骼"),
                    "controllable", Schema.Bool("允许手动操作"),
                    "rotatable", Schema.Bool("旋转标志"),
                    "translatable", Schema.Bool("平移标志"),
                    "after_physics", Schema.Bool("在物理计算后变形"),
                    "append_rotation", Schema.Bool("从追加父骨骼继承旋转"),
                    "append_translation", Schema.Bool("从追加父骨骼继承平移"),
                    "append_local", Schema.Bool("以父骨骼的局部坐标系计算追加值"),
                    "append_parent", Schema.Int("追加父骨骼索引，-1 表示无"),
                    "append_parent_name", Schema.Str("追加父骨骼名称"),
                    "append_ratio", Schema.Num("追加比例"),
                    "fix_axis", Schema.Bool("限制为仅沿单一轴操作"),
                    "fix_axis_vector", Schema.NumArray("固定轴方向 [x, y, z]", 3),
                    "local_frame", Schema.Bool("为该骨骼启用独立操作坐标系"),
                    "local_x", Schema.NumArray("局部坐标系 X 轴 [x, y, z]", 3),
                    "local_z", Schema.NumArray("局部坐标系 Z 轴 [x, y, z]", 3),
                    "external", Schema.Bool("由模型外部的父级驱动变形"),
                    "external_key", Schema.Int("外部父级键值"),
                    "is_ik", Schema.Bool("将该骨骼设为 IK 骨骼"))),
                false,
                delegate(Dictionary<string, object> args) { return SetBone(editor, args); });

            registry.Add(
                "set_bone_ik",
                "编辑单个骨骼的 IK 设置：目标、循环次数、每次循环的角度限制以及整条链接链。"
                    + "角度单位为弧度，PMX 中的数值是 PMD 原始值的四倍。传入 links 会整体替换链接链；"
                    + "每项为 {\"bone\": 索引} 或 {\"bone_name\": 名称}，还可加 "
                    + "{\"low\": [x,y,z], \"high\": [x,y,z]}（弧度）来限制该链接。",
                Schema.Object(Json.Obj(
                    "index", Schema.Int("IK 骨骼的索引"),
                    "name", Schema.Str("IK 骨骼的日文名"),
                    "target", Schema.Int("目标骨骼索引（向 IK 骨骼伸出的尾端骨骼）"),
                    "target_name", Schema.Str("目标骨骼名称"),
                    "loop_count", Schema.Int("求解迭代次数"),
                    "angle", Schema.Num("每次迭代的角度限制，单位弧度"),
                    "links", Schema.ObjArray("替换用的链接链，尾端在前"))),
                false,
                delegate(Dictionary<string, object> args) { return SetBoneIk(editor, args); });

            registry.Add(
                "add_bone",
                "添加一个骨骼并返回其索引。未传入的属性保持编辑器默认值。",
                Schema.Object(Json.Obj(
                    "new_name", Schema.Str("日文名"),
                    "new_name_en", Schema.Str("英文名"),
                    "position", Schema.NumArray("骨骼位置 [x, y, z]", 3),
                    "parent", Schema.Int("父骨骼索引，-1 表示无"),
                    "parent_name", Schema.Str("父骨骼名称"),
                    "level", Schema.Int("变形层级"),
                    "visible", Schema.Bool("在编辑器中显示该骨骼"),
                    "controllable", Schema.Bool("允许手动操作"),
                    "rotatable", Schema.Bool("旋转标志"),
                    "translatable", Schema.Bool("平移标志"))),
                false,
                delegate(Dictionary<string, object> args) { return AddBone(editor, args); });

            registry.Add(
                "delete_bone",
                "删除一个骨骼。以该骨骼为权重的顶点以及其他引用它的对象会变成空引用，"
                    + "因此仅用于确认未使用的骨骼。",
                Schema.Object(Json.Obj(
                    "index", Schema.Int("骨骼索引"),
                    "name", Schema.Str("日文骨骼名"))),
                false,
                delegate(Dictionary<string, object> args) { return DeleteBone(editor, args); });
        }

        private static object ListBones(Editor editor, Dictionary<string, object> args)
        {
            return editor.Read<object>(delegate(IPXPmx pmx)
            {
                string filter = Json.Str(args, "name_contains", null);
                List<object> rows = new List<object>();

                List<int> matched = new List<int>();
                for (int i = 0; i < pmx.Bone.Count; i++)
                {
                    IPXBone bone = pmx.Bone[i];
                    if (filter == null || PmxUtil.Matches(bone.Name, filter) || PmxUtil.Matches(bone.NameE, filter))
                    {
                        matched.Add(i);
                    }
                }

                int offset, limit;
                PmxUtil.Page(args, matched.Count, 200, out offset, out limit);

                for (int n = offset; n < offset + limit; n++)
                {
                    int i = matched[n];
                    IPXBone bone = pmx.Bone[i];
                    rows.Add(Json.Obj(
                        "index", i,
                        "name", bone.Name,
                        "nameEn", bone.NameE,
                        "parentIndex", PmxUtil.IndexOf(pmx.Bone, bone.Parent),
                        "parentName", bone.Parent == null ? null : bone.Parent.Name,
                        "position", PmxUtil.Vec3(bone.Position),
                        "level", bone.Level,
                        "visible", bone.Visible,
                        "isIK", bone.IsIK));
                }

                return Json.Obj(
                    "total", pmx.Bone.Count,
                    "matched", matched.Count,
                    "offset", offset,
                    "count", rows.Count,
                    "bones", rows.ToArray());
            });
        }

        private static object Detail(IPXPmx pmx, int index)
        {
            IPXBone bone = pmx.Bone[index];

            Dictionary<string, object> result = Json.Obj(
                "index", index,
                "name", bone.Name,
                "nameEn", bone.NameE,
                "position", PmxUtil.Vec3(bone.Position),
                "parentIndex", PmxUtil.IndexOf(pmx.Bone, bone.Parent),
                "parentName", bone.Parent == null ? null : bone.Parent.Name,
                "toBoneIndex", PmxUtil.IndexOf(pmx.Bone, bone.ToBone),
                "toOffset", PmxUtil.Vec3(bone.ToOffset),
                "level", bone.Level,
                "flags", Json.Obj(
                    "rotatable", bone.IsRotation,
                    "translatable", bone.IsTranslation,
                    "visible", bone.Visible,
                    "controllable", bone.Controllable,
                    "isIK", bone.IsIK,
                    "appendRotation", bone.IsAppendRotation,
                    "appendTranslation", bone.IsAppendTranslation,
                    "appendLocal", bone.IsAppendLocal,
                    "fixAxis", bone.IsFixAxis,
                    "localFrame", bone.IsLocalFrame,
                    "afterPhysics", bone.IsAfterPhysics,
                    "external", bone.IsExternal));

            if (bone.IsAppendRotation || bone.IsAppendTranslation)
            {
                result["appendParentIndex"] = PmxUtil.IndexOf(pmx.Bone, bone.AppendParent);
                result["appendParentName"] = bone.AppendParent == null ? null : bone.AppendParent.Name;
                result["appendRatio"] = bone.AppendRatio;
            }
            if (bone.IsFixAxis)
            {
                result["fixAxisVector"] = PmxUtil.Vec3(bone.FixAxis);
            }
            if (bone.IsLocalFrame)
            {
                // Not on the interface, so it may be absent on some builds; report what
                // is there rather than pretending the axes do not exist.
                result["localX"] = PmxUtil.Vec3(PmxRef.LocalAxis(bone, "LocalX"));
                result["localY"] = PmxUtil.Vec3(PmxRef.LocalAxis(bone, "LocalY"));
                result["localZ"] = PmxUtil.Vec3(PmxRef.LocalAxis(bone, "LocalZ"));
            }
            if (bone.IsExternal)
            {
                result["externalKey"] = bone.ExternalKey;
            }
            if (bone.IsIK && bone.IK != null)
            {
                List<object> links = new List<object>();
                foreach (IPXIKLink link in bone.IK.Links)
                {
                    links.Add(Json.Obj(
                        "boneIndex", PmxUtil.IndexOf(pmx.Bone, link.Bone),
                        "boneName", link.Bone == null ? null : link.Bone.Name,
                        "isLimit", link.IsLimit,
                        "low", PmxUtil.Vec3(link.Low),
                        "high", PmxUtil.Vec3(link.High)));
                }
                result["ik"] = Json.Obj(
                    "targetIndex", PmxUtil.IndexOf(pmx.Bone, bone.IK.Target),
                    "targetName", bone.IK.Target == null ? null : bone.IK.Target.Name,
                    "loopCount", bone.IK.LoopCount,
                    "angle", bone.IK.Angle,
                    "links", links.ToArray());
            }
            return result;
        }

        private static object GetBone(Editor editor, Dictionary<string, object> args)
        {
            return editor.Read<object>(delegate(IPXPmx pmx)
            {
                return Detail(pmx, PmxUtil.ResolveBone(pmx, args));
            });
        }

        /// <summary>Applies every writable bone field present in the arguments.</summary>
        private static void Apply(IPXPmx pmx, IPXBone bone, Dictionary<string, object> args)
        {
            if (Json.Has(args, "new_name")) bone.Name = Json.Str(args, "new_name", bone.Name);
            if (Json.Has(args, "new_name_en")) bone.NameE = Json.Str(args, "new_name_en", bone.NameE);
            if (Json.Has(args, "visible")) bone.Visible = Json.Bool(args, "visible", bone.Visible);
            if (Json.Has(args, "controllable")) bone.Controllable = Json.Bool(args, "controllable", bone.Controllable);
            if (Json.Has(args, "rotatable")) bone.IsRotation = Json.Bool(args, "rotatable", bone.IsRotation);
            if (Json.Has(args, "translatable")) bone.IsTranslation = Json.Bool(args, "translatable", bone.IsTranslation);
            if (Json.Has(args, "after_physics")) bone.IsAfterPhysics = Json.Bool(args, "after_physics", bone.IsAfterPhysics);
            if (Json.Has(args, "level")) bone.Level = Json.Int(args, "level", bone.Level);

            float[] a = Json.Floats(args, "position", 3);
            if (a != null) bone.Position = PmxRef.V3Of(a);

            bool given;
            IPXBone other = PmxRef.BoneArg(pmx, args, "parent", out given);
            if (given)
            {
                if (ReferenceEquals(other, bone))
                {
                    throw new McpToolException("a bone cannot be its own parent");
                }
                bone.Parent = other;
            }

            other = PmxRef.BoneArg(pmx, args, "to_bone", out given);
            if (given) bone.ToBone = other;

            a = Json.Floats(args, "to_offset", 3);
            if (a != null)
            {
                // The tip is either a bone or an offset, never both.
                bone.ToBone = null;
                bone.ToOffset = PmxRef.V3Of(a);
            }

            // Append (what MMD calls 付与): the flags first, so a parent set in the same
            // call is not dropped by a later flag change.
            if (Json.Has(args, "append_rotation"))
                bone.IsAppendRotation = Json.Bool(args, "append_rotation", bone.IsAppendRotation);
            if (Json.Has(args, "append_translation"))
                bone.IsAppendTranslation = Json.Bool(args, "append_translation", bone.IsAppendTranslation);
            if (Json.Has(args, "append_local"))
                bone.IsAppendLocal = Json.Bool(args, "append_local", bone.IsAppendLocal);
            other = PmxRef.BoneArg(pmx, args, "append_parent", out given);
            if (given) bone.AppendParent = other;
            if (Json.Has(args, "append_ratio"))
                bone.AppendRatio = Json.Flt(args, "append_ratio", bone.AppendRatio);

            if (Json.Has(args, "fix_axis")) bone.IsFixAxis = Json.Bool(args, "fix_axis", bone.IsFixAxis);
            a = Json.Floats(args, "fix_axis_vector", 3);
            if (a != null) bone.FixAxis = PmxRef.V3Of(a);

            if (Json.Has(args, "local_frame")) bone.IsLocalFrame = Json.Bool(args, "local_frame", bone.IsLocalFrame);

            // The frame is written as a whole: X and Z are what PMX stores, and Y is
            // derived from them. Passing only one keeps the other as it stands.
            float[] lx = Json.Floats(args, "local_x", 3);
            float[] lz = Json.Floats(args, "local_z", 3);
            if (lx != null || lz != null)
            {
                V3 ax = lx != null ? PmxRef.V3Of(lx) : PmxRef.LocalAxis(bone, "LocalX");
                V3 az = lz != null ? PmxRef.V3Of(lz) : PmxRef.LocalAxis(bone, "LocalZ");
                if (ax == null || az == null)
                {
                    throw new McpToolException("this PMX Editor build does not expose the local frame axes");
                }
                if (!PmxRef.SetLocalFrame(bone, ax, az))
                {
                    throw new McpToolException("the local frame could not be written on this build");
                }
            }

            if (Json.Has(args, "external")) bone.IsExternal = Json.Bool(args, "external", bone.IsExternal);
            if (Json.Has(args, "external_key")) bone.ExternalKey = Json.Int(args, "external_key", bone.ExternalKey);
            if (Json.Has(args, "is_ik")) bone.IsIK = Json.Bool(args, "is_ik", bone.IsIK);
        }

        private static object SetBone(Editor editor, Dictionary<string, object> args)
        {
            return editor.Edit<object>(delegate(IPXPmx pmx, Editor.Change change)
            {
                change.Target = PmxUpdateObject.Bone;
                change.ListTarget = PEPlugin.Pmd.UpdateObject.Bone;

                int index = PmxUtil.ResolveBone(pmx, args);
                Apply(pmx, pmx.Bone[index], args);
                change.Index = index;
                return Detail(pmx, index);
            });
        }

        private static object SetBoneIk(Editor editor, Dictionary<string, object> args)
        {
            return editor.Edit<object>(delegate(IPXPmx pmx, Editor.Change change)
            {
                change.Target = PmxUpdateObject.Bone;
                change.ListTarget = PEPlugin.Pmd.UpdateObject.Bone;

                int index = PmxUtil.ResolveBone(pmx, args);
                IPXBone bone = pmx.Bone[index];

                // Editing IK on a bone that is not an IK bone would silently do nothing,
                // so turn the flag on rather than leaving the caller puzzled.
                if (!bone.IsIK) bone.IsIK = true;
                if (bone.IK == null)
                {
                    throw new McpToolException("this bone has no IK block to edit");
                }

                bool given;
                IPXBone target = PmxRef.BoneArg(pmx, args, "target", out given);
                if (given) bone.IK.Target = target;

                if (Json.Has(args, "loop_count")) bone.IK.LoopCount = Json.Int(args, "loop_count", bone.IK.LoopCount);
                if (Json.Has(args, "angle")) bone.IK.Angle = Json.Flt(args, "angle", bone.IK.Angle);

                object[] links = Json.Arr(args, "links");
                if (links != null)
                {
                    bone.IK.Links.Clear();
                    foreach (object raw in links)
                    {
                        Dictionary<string, object> item = raw as Dictionary<string, object>;
                        if (item == null) throw new McpToolException("each entry of links must be an object");

                        bool has;
                        IPXBone linkBone = PmxRef.BoneArg(pmx, item, "bone", out has);
                        if (!has || linkBone == null)
                        {
                            throw new McpToolException("each entry of links needs bone or bone_name");
                        }

                        float[] low = Json.Floats(item, "low", 3);
                        float[] high = Json.Floats(item, "high", 3);
                        IPXIKLink link;
                        if (low != null && high != null)
                        {
                            link = PmxRef.Builder.IKLink(linkBone, PmxRef.V3Of(low), PmxRef.V3Of(high));
                            link.IsLimit = true;
                        }
                        else
                        {
                            link = PmxRef.Builder.IKLink(linkBone);
                            link.IsLimit = false;
                        }
                        bone.IK.Links.Add(link);
                    }
                }

                change.Index = index;
                return Detail(pmx, index);
            });
        }

        private static object AddBone(Editor editor, Dictionary<string, object> args)
        {
            return editor.Edit<object>(delegate(IPXPmx pmx, Editor.Change change)
            {
                change.Target = PmxUpdateObject.Bone;
                change.ListTarget = PEPlugin.Pmd.UpdateObject.Bone;

                IPXBone bone = PmxRef.Builder.Bone();
                bone.Name = Json.Str(args, "new_name", "新規ボーン");
                bone.NameE = Json.Str(args, "new_name_en", "");
                Apply(pmx, bone, args);
                pmx.Bone.Add(bone);

                // 足したときは番号を渡さない。向こうの一覧はまだ増えていないので、
                // 新しい番号は範囲の外になる(既定の -1 で全体を作り直させる)
                return Detail(pmx, pmx.Bone.Count - 1);
            });
        }

        private static object DeleteBone(Editor editor, Dictionary<string, object> args)
        {
            return editor.Edit<object>(delegate(IPXPmx pmx, Editor.Change change)
            {
                change.Target = PmxUpdateObject.Bone;
                change.ListTarget = PEPlugin.Pmd.UpdateObject.Bone;

                int index = PmxUtil.ResolveBone(pmx, args);
                string name = pmx.Bone[index].Name;
                pmx.Bone.RemoveAt(index);
                return Json.Obj("deleted", name, "index", index, "remaining", pmx.Bone.Count);
            });
        }
    }
}
