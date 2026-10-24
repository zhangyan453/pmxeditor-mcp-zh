using System;
using System.Collections.Generic;
using PEPlugin.Pmd;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxMcp
{
    /// <summary>
    /// Rigid bodies, joints and soft bodies.
    ///
    /// These are the parts MMD can hand back to a PMX, so they are worth being able to
    /// read and write in full.  Group membership is a 16-slot mask: "group" is the group
    /// this body belongs to (0-15) and "passGroup" is the 16 booleans saying which groups
    /// it does *not* collide with, which is how PMX stores it.
    /// </summary>
    internal static class PhysicsTools
    {
        public static void Register(ToolRegistry registry, Editor editor)
        {
            // ---- rigid bodies -------------------------------------------------------

            registry.Add(
                "list_bodies",
                "列出刚体及其骨骼、模式、形状和位置。分页返回，可按名称筛选。",
                Schema.Object(Json.Obj(
                    "offset", Schema.Int("返回的起始刚体索引（默认 0）"),
                    "limit", Schema.Int("返回的刚体数量（默认 200，最大 1000）"),
                    "name_contains", Schema.Str("仅返回日文名或英文名包含此文本的刚体"))),
                true,
                delegate(Dictionary<string, object> args) { return ListBodies(editor, args); });

            registry.Add(
                "get_body",
                "查看单个刚体的完整信息，包括质量、阻尼和碰撞组。",
                Schema.Object(Json.Obj(
                    "index", Schema.Int("刚体索引"),
                    "name", Schema.Str("日文刚体名"))),
                true,
                delegate(Dictionary<string, object> args) { return GetBody(editor, args); });

            registry.Add(
                "set_body",
                "编辑单个刚体。只修改传入的字段。"
                    + "mode 为以下之一：" + Modes + "；shape 为以下之一：" + Shapes + "。"
                    + "旋转单位为弧度。",
                Schema.Object(Json.Obj(
                    "index", Schema.Int("刚体索引"),
                    "name", Schema.Str("用于查找刚体的日文名"),
                    "new_name", Schema.Str("新的日文名"),
                    "new_name_en", Schema.Str("新的英文名"),
                    "bone", Schema.Int("刚体跟随的骨骼索引，-1 表示无"),
                    "bone_name", Schema.Str("刚体跟随的骨骼名称"),
                    "mode", Schema.Str("物理模式：" + Modes),
                    "shape", Schema.Str("碰撞形状：" + Shapes),
                    "size", Schema.NumArray("形状尺寸 [x, y, z]", 3),
                    "position", Schema.NumArray("位置 [x, y, z]", 3),
                    "rotation", Schema.NumArray("旋转（弧度）[x, y, z]", 3),
                    "group", Schema.Int("碰撞组，0-15"),
                    "pass_group", Schema.BoolArray("16 个布尔值：该刚体不与之碰撞的组", 16),
                    "mass", Schema.Num("质量"),
                    "position_damping", Schema.Num("移动阻尼"),
                    "rotation_damping", Schema.Num("旋转阻尼"),
                    "restitution", Schema.Num("弹性（回弹）"),
                    "friction", Schema.Num("摩擦"))),
                false,
                delegate(Dictionary<string, object> args) { return SetBody(editor, args); });

            registry.Add(
                "add_body",
                "添加一个刚体并返回其索引。未传入的属性保持编辑器默认值，"
                    + "因此只给名称和骨骼即可创建新刚体。",
                Schema.Object(Json.Obj(
                    "new_name", Schema.Str("日文名"),
                    "new_name_en", Schema.Str("英文名"),
                    "bone", Schema.Int("刚体跟随的骨骼索引，-1 表示无"),
                    "bone_name", Schema.Str("刚体跟随的骨骼名称"),
                    "mode", Schema.Str("物理模式：" + Modes),
                    "shape", Schema.Str("碰撞形状：" + Shapes),
                    "size", Schema.NumArray("形状尺寸 [x, y, z]", 3),
                    "position", Schema.NumArray("位置 [x, y, z]", 3),
                    "rotation", Schema.NumArray("旋转（弧度）[x, y, z]", 3),
                    "group", Schema.Int("碰撞组，0-15"),
                    "mass", Schema.Num("质量"))),
                false,
                delegate(Dictionary<string, object> args) { return AddBody(editor, args); });

            registry.Add(
                "delete_body",
                "删除一个刚体。引用它的 Joint 会变成空引用，请随后检查 list_joints。",
                Schema.Object(Json.Obj(
                    "index", Schema.Int("刚体索引"),
                    "name", Schema.Str("日文刚体名"))),
                false,
                delegate(Dictionary<string, object> args) { return DeleteBody(editor, args); });

            // ---- joints -------------------------------------------------------------

            registry.Add(
                "list_joints",
                "列出 Joint 及其连接的两个刚体和位置。分页返回。",
                Schema.Object(Json.Obj(
                    "offset", Schema.Int("返回的起始 Joint 索引（默认 0）"),
                    "limit", Schema.Int("返回的 Joint 数量（默认 200，最大 1000）"),
                    "name_contains", Schema.Str("仅返回日文名或英文名包含此文本的 Joint"))),
                true,
                delegate(Dictionary<string, object> args) { return ListJoints(editor, args); });

            registry.Add(
                "get_joint",
                "查看单个 Joint 的完整信息，包括移动和角度限制以及弹簧常数。",
                Schema.Object(Json.Obj(
                    "index", Schema.Int("Joint 索引"),
                    "name", Schema.Str("日文 Joint 名"))),
                true,
                delegate(Dictionary<string, object> args) { return GetJoint(editor, args); });

            registry.Add(
                "set_joint",
                "编辑单个 Joint。只修改传入的字段。kind 为以下之一：" + Kinds
                    + "。角度单位为弧度。",
                Schema.Object(Json.Obj(
                    "index", Schema.Int("Joint 索引"),
                    "name", Schema.Str("用于查找 Joint 的日文名"),
                    "new_name", Schema.Str("新的日文名"),
                    "new_name_en", Schema.Str("新的英文名"),
                    "body_a", Schema.Int("第一个刚体的索引，-1 表示无"),
                    "body_b", Schema.Int("第二个刚体的索引，-1 表示无"),
                    "kind", Schema.Str("Joint 类型：" + Kinds),
                    "position", Schema.NumArray("位置 [x, y, z]", 3),
                    "rotation", Schema.NumArray("旋转（弧度）[x, y, z]", 3),
                    "move_low", Schema.NumArray("移动下限 [x, y, z]", 3),
                    "move_high", Schema.NumArray("移动上限 [x, y, z]", 3),
                    "angle_low", Schema.NumArray("角度下限（弧度）[x, y, z]", 3),
                    "angle_high", Schema.NumArray("角度上限（弧度）[x, y, z]", 3),
                    "spring_move", Schema.NumArray("移动弹簧常数 [x, y, z]", 3),
                    "spring_rotate", Schema.NumArray("旋转弹簧常数 [x, y, z]", 3))),
                false,
                delegate(Dictionary<string, object> args) { return SetJoint(editor, args); });

            registry.Add(
                "add_joint",
                "添加一个 Joint 并返回其索引。",
                Schema.Object(Json.Obj(
                    "new_name", Schema.Str("日文名"),
                    "new_name_en", Schema.Str("英文名"),
                    "body_a", Schema.Int("第一个刚体的索引"),
                    "body_b", Schema.Int("第二个刚体的索引"),
                    "kind", Schema.Str("Joint 类型：" + Kinds),
                    "position", Schema.NumArray("位置 [x, y, z]", 3),
                    "rotation", Schema.NumArray("旋转（弧度）[x, y, z]", 3))),
                false,
                delegate(Dictionary<string, object> args) { return AddJoint(editor, args); });

            registry.Add(
                "delete_joint",
                "删除一个 Joint。",
                Schema.Object(Json.Obj(
                    "index", Schema.Int("Joint 索引"),
                    "name", Schema.Str("日文 Joint 名"))),
                false,
                delegate(Dictionary<string, object> args) { return DeleteJoint(editor, args); });

            // ---- soft bodies --------------------------------------------------------

            registry.Add(
                "list_soft_bodies",
                "列出 SoftBody。仅 PMX 2.1 支持；多数模型没有。",
                Schema.Object(Json.Obj(
                    "offset", Schema.Int("返回的起始 SoftBody 索引（默认 0）"),
                    "limit", Schema.Int("返回的数量（默认 200，最大 1000）"))),
                true,
                delegate(Dictionary<string, object> args) { return ListSoftBodies(editor, args); });

            registry.Add(
                "get_soft_body",
                "查看单个 SoftBody 的完整信息，包括所有模拟系数。",
                Schema.Object(Json.Obj(
                    "index", Schema.Int("SoftBody 索引"),
                    "name", Schema.Str("日文 SoftBody 名"))),
                true,
                delegate(Dictionary<string, object> args) { return GetSoftBody(editor, args); });

            registry.Add(
                "set_soft_body",
                "编辑单个 SoftBody。只修改传入的字段。shape 为以下之一："
                    + SoftShapes + "。单字母系数沿用 PMX 2.1 的命名。",
                Schema.Object(Json.Obj(
                    "index", Schema.Int("SoftBody 索引"),
                    "name", Schema.Str("用于查找 SoftBody 的日文名"),
                    "new_name", Schema.Str("新的日文名"),
                    "new_name_en", Schema.Str("新的英文名"),
                    "shape", Schema.Str("形状：" + SoftShapes),
                    "material", Schema.Int("SoftBody 所基于的材质索引"),
                    "group", Schema.Int("碰撞组，0-15"),
                    "total_mass", Schema.Num("总质量"),
                    "margin", Schema.Num("碰撞边距"),
                    "aero_model", Schema.Int("空气动力学模型"),
                    "cluster_count", Schema.Int("簇数量"),
                    "bending_link_distance", Schema.Int("弯曲链接距离"),
                    "generate_bending_links", Schema.Bool("生成弯曲链接"),
                    "generate_clusters", Schema.Bool("生成簇"),
                    "randomize_constraints", Schema.Bool("随机化约束"),
                    "coefficients", Schema.Any(
                        "以下任一项可作为数值：VCF、DP、DG、LF、PR、VC、DF、MT、CHR、KHR、SHR、AHR、"
                        + "SRHR_CL、SKHR_CL、SSHR_CL、SR_SPLT_CL、SK_SPLT_CL、SS_SPLT_CL、"
                        + "LST、AST、VST；以下项为整数：V_IT、P_IT、D_IT、C_IT"))),
                false,
                delegate(Dictionary<string, object> args) { return SetSoftBody(editor, args); });
        }

        private static readonly string Modes = PmxRef.Choices(typeof(BodyMode));
        private static readonly string Shapes = PmxRef.Choices(typeof(BodyBoxKind));
        private static readonly string Kinds = PmxRef.Choices(typeof(JointKind));
        private static readonly string SoftShapes = PmxRef.Choices(typeof(SoftBodyShape));

        // ---- rigid bodies -----------------------------------------------------------

        private static object BodyRow(IPXPmx pmx, int i, IPXBody b, bool full)
        {
            Dictionary<string, object> row = Json.Obj(
                "index", i,
                "name", b.Name,
                "nameEn", b.NameE,
                "boneIndex", PmxUtil.IndexOf(pmx.Bone, b.Bone),
                "boneName", b.Bone == null ? null : b.Bone.Name,
                "mode", b.Mode.ToString(),
                "shape", b.BoxKind.ToString(),
                "size", PmxUtil.Vec3(b.BoxSize),
                "position", PmxUtil.Vec3(b.Position),
                "rotation", PmxUtil.Vec3(b.Rotation),
                "group", b.Group);
            if (full)
            {
                row["passGroup"] = Bools(b.PassGroup);
                row["mass"] = b.Mass;
                row["positionDamping"] = b.PositionDamping;
                row["rotationDamping"] = b.RotationDamping;
                row["restitution"] = b.Restitution;
                row["friction"] = b.Friction;
            }
            return row;
        }

        private static object[] Bools(bool[] a)
        {
            if (a == null) return null;
            object[] o = new object[a.Length];
            for (int i = 0; i < a.Length; i++) o[i] = a[i];
            return o;
        }

        private static object ListBodies(Editor editor, Dictionary<string, object> args)
        {
            return editor.Read<object>(delegate(IPXPmx pmx)
            {
                string filter = Json.Str(args, "name_contains", null);
                List<int> matched = new List<int>();
                for (int i = 0; i < pmx.Body.Count; i++)
                {
                    if (filter == null || PmxUtil.Matches(pmx.Body[i].Name, filter)
                                       || PmxUtil.Matches(pmx.Body[i].NameE, filter))
                    {
                        matched.Add(i);
                    }
                }

                int offset, limit;
                PmxUtil.Page(args, matched.Count, 200, out offset, out limit);

                List<object> rows = new List<object>();
                for (int n = offset; n < offset + limit; n++)
                {
                    rows.Add(BodyRow(pmx, matched[n], pmx.Body[matched[n]], false));
                }
                return Json.Obj(
                    "total", pmx.Body.Count,
                    "matched", matched.Count,
                    "offset", offset,
                    "count", rows.Count,
                    "bodies", rows.ToArray());
            });
        }

        private static object GetBody(Editor editor, Dictionary<string, object> args)
        {
            return editor.Read<object>(delegate(IPXPmx pmx)
            {
                int i = PmxRef.ResolveBody(pmx, args);
                return BodyRow(pmx, i, pmx.Body[i], true);
            });
        }

        /// <summary>Applies the shared body fields; used by both set_body and add_body.</summary>
        private static void ApplyBody(IPXPmx pmx, IPXBody b, Dictionary<string, object> args)
        {
            if (Json.Has(args, "new_name")) b.Name = Json.Str(args, "new_name", b.Name);
            if (Json.Has(args, "new_name_en")) b.NameE = Json.Str(args, "new_name_en", b.NameE);

            bool given;
            IPXBone bone = PmxRef.BoneArg(pmx, args, "bone", out given);
            if (given) b.Bone = bone;

            b.Mode = PmxRef.EnumArg(args, "mode", b.Mode);
            b.BoxKind = PmxRef.EnumArg(args, "shape", b.BoxKind);

            float[] a;
            a = Json.Floats(args, "size", 3); if (a != null) b.BoxSize = PmxRef.V3Of(a);
            a = Json.Floats(args, "position", 3); if (a != null) b.Position = PmxRef.V3Of(a);
            a = Json.Floats(args, "rotation", 3); if (a != null) b.Rotation = PmxRef.V3Of(a);

            if (Json.Has(args, "group")) b.Group = Json.Int(args, "group", b.Group);
            if (Json.Has(args, "mass")) b.Mass = Json.Flt(args, "mass", b.Mass);
            if (Json.Has(args, "position_damping")) b.PositionDamping = Json.Flt(args, "position_damping", b.PositionDamping);
            if (Json.Has(args, "rotation_damping")) b.RotationDamping = Json.Flt(args, "rotation_damping", b.RotationDamping);
            if (Json.Has(args, "restitution")) b.Restitution = Json.Flt(args, "restitution", b.Restitution);
            if (Json.Has(args, "friction")) b.Friction = Json.Flt(args, "friction", b.Friction);

            object[] pass = Json.Arr(args, "pass_group");
            if (pass != null && b.PassGroup != null)
            {
                // PassGroup is a fixed 16-slot array owned by the body, so it is written
                // in place rather than replaced.
                for (int i = 0; i < pass.Length && i < b.PassGroup.Length; i++)
                {
                    b.PassGroup[i] = Convert.ToBoolean(pass[i]);
                }
            }
        }

        private static object SetBody(Editor editor, Dictionary<string, object> args)
        {
            return editor.Edit<object>(delegate(IPXPmx pmx, Editor.Change change)
            {
                change.Target = PmxUpdateObject.Body;
                change.ListTarget = PEPlugin.Pmd.UpdateObject.Body;

                int i = PmxRef.ResolveBody(pmx, args);
                ApplyBody(pmx, pmx.Body[i], args);
                change.Index = i;
                return BodyRow(pmx, i, pmx.Body[i], true);
            });
        }

        private static object AddBody(Editor editor, Dictionary<string, object> args)
        {
            return editor.Edit<object>(delegate(IPXPmx pmx, Editor.Change change)
            {
                change.Target = PmxUpdateObject.Body;
                change.ListTarget = PEPlugin.Pmd.UpdateObject.Body;

                IPXBody b = PmxRef.Builder.Body();
                ApplyBody(pmx, b, args);
                pmx.Body.Add(b);
                // 足したときは番号を渡さない。向こうの一覧はまだ増えていないので、
                // 新しい番号は範囲の外になる(既定の -1 で全体を作り直させる)
                int i = pmx.Body.Count - 1;
                return BodyRow(pmx, i, b, true);
            });
        }

        private static object DeleteBody(Editor editor, Dictionary<string, object> args)
        {
            return editor.Edit<object>(delegate(IPXPmx pmx, Editor.Change change)
            {
                change.Target = PmxUpdateObject.Body;
                change.ListTarget = PEPlugin.Pmd.UpdateObject.Body;

                int i = PmxRef.ResolveBody(pmx, args);
                string name = pmx.Body[i].Name;
                pmx.Body.RemoveAt(i);
                return Json.Obj("deleted", name, "index", i, "remaining", pmx.Body.Count);
            });
        }

        // ---- joints -----------------------------------------------------------------

        private static object JointRow(IPXPmx pmx, int i, IPXJoint j, bool full)
        {
            Dictionary<string, object> row = Json.Obj(
                "index", i,
                "name", j.Name,
                "nameEn", j.NameE,
                "kind", j.Kind.ToString(),
                "bodyAIndex", PmxUtil.IndexOf(pmx.Body, j.BodyA),
                "bodyAName", j.BodyA == null ? null : j.BodyA.Name,
                "bodyBIndex", PmxUtil.IndexOf(pmx.Body, j.BodyB),
                "bodyBName", j.BodyB == null ? null : j.BodyB.Name,
                "position", PmxUtil.Vec3(j.Position),
                "rotation", PmxUtil.Vec3(j.Rotation));
            if (full)
            {
                row["moveLow"] = PmxUtil.Vec3(j.Limit_MoveLow);
                row["moveHigh"] = PmxUtil.Vec3(j.Limit_MoveHigh);
                row["angleLow"] = PmxUtil.Vec3(j.Limit_AngleLow);
                row["angleHigh"] = PmxUtil.Vec3(j.Limit_AngleHigh);
                row["springMove"] = PmxUtil.Vec3(j.SpringConst_Move);
                row["springRotate"] = PmxUtil.Vec3(j.SpringConst_Rotate);
            }
            return row;
        }

        private static object ListJoints(Editor editor, Dictionary<string, object> args)
        {
            return editor.Read<object>(delegate(IPXPmx pmx)
            {
                string filter = Json.Str(args, "name_contains", null);
                List<int> matched = new List<int>();
                for (int i = 0; i < pmx.Joint.Count; i++)
                {
                    if (filter == null || PmxUtil.Matches(pmx.Joint[i].Name, filter)
                                       || PmxUtil.Matches(pmx.Joint[i].NameE, filter))
                    {
                        matched.Add(i);
                    }
                }

                int offset, limit;
                PmxUtil.Page(args, matched.Count, 200, out offset, out limit);

                List<object> rows = new List<object>();
                for (int n = offset; n < offset + limit; n++)
                {
                    rows.Add(JointRow(pmx, matched[n], pmx.Joint[matched[n]], false));
                }
                return Json.Obj(
                    "total", pmx.Joint.Count,
                    "matched", matched.Count,
                    "offset", offset,
                    "count", rows.Count,
                    "joints", rows.ToArray());
            });
        }

        private static object GetJoint(Editor editor, Dictionary<string, object> args)
        {
            return editor.Read<object>(delegate(IPXPmx pmx)
            {
                int i = PmxRef.ResolveJoint(pmx, args);
                return JointRow(pmx, i, pmx.Joint[i], true);
            });
        }

        private static void ApplyJoint(IPXPmx pmx, IPXJoint j, Dictionary<string, object> args)
        {
            if (Json.Has(args, "new_name")) j.Name = Json.Str(args, "new_name", j.Name);
            if (Json.Has(args, "new_name_en")) j.NameE = Json.Str(args, "new_name_en", j.NameE);

            bool given;
            IPXBody a = PmxRef.BodyArg(pmx, args, "body_a", out given); if (given) j.BodyA = a;
            IPXBody b = PmxRef.BodyArg(pmx, args, "body_b", out given); if (given) j.BodyB = b;

            j.Kind = PmxRef.EnumArg(args, "kind", j.Kind);

            float[] v;
            v = Json.Floats(args, "position", 3); if (v != null) j.Position = PmxRef.V3Of(v);
            v = Json.Floats(args, "rotation", 3); if (v != null) j.Rotation = PmxRef.V3Of(v);
            v = Json.Floats(args, "move_low", 3); if (v != null) j.Limit_MoveLow = PmxRef.V3Of(v);
            v = Json.Floats(args, "move_high", 3); if (v != null) j.Limit_MoveHigh = PmxRef.V3Of(v);
            v = Json.Floats(args, "angle_low", 3); if (v != null) j.Limit_AngleLow = PmxRef.V3Of(v);
            v = Json.Floats(args, "angle_high", 3); if (v != null) j.Limit_AngleHigh = PmxRef.V3Of(v);
            v = Json.Floats(args, "spring_move", 3); if (v != null) j.SpringConst_Move = PmxRef.V3Of(v);
            v = Json.Floats(args, "spring_rotate", 3); if (v != null) j.SpringConst_Rotate = PmxRef.V3Of(v);
        }

        private static object SetJoint(Editor editor, Dictionary<string, object> args)
        {
            return editor.Edit<object>(delegate(IPXPmx pmx, Editor.Change change)
            {
                change.Target = PmxUpdateObject.Joint;
                change.ListTarget = PEPlugin.Pmd.UpdateObject.Joint;

                int i = PmxRef.ResolveJoint(pmx, args);
                ApplyJoint(pmx, pmx.Joint[i], args);
                change.Index = i;
                return JointRow(pmx, i, pmx.Joint[i], true);
            });
        }

        private static object AddJoint(Editor editor, Dictionary<string, object> args)
        {
            return editor.Edit<object>(delegate(IPXPmx pmx, Editor.Change change)
            {
                change.Target = PmxUpdateObject.Joint;
                change.ListTarget = PEPlugin.Pmd.UpdateObject.Joint;

                IPXJoint j = PmxRef.Builder.Joint();
                ApplyJoint(pmx, j, args);
                pmx.Joint.Add(j);
                int i = pmx.Joint.Count - 1;
                return JointRow(pmx, i, j, true);
            });
        }

        private static object DeleteJoint(Editor editor, Dictionary<string, object> args)
        {
            return editor.Edit<object>(delegate(IPXPmx pmx, Editor.Change change)
            {
                change.Target = PmxUpdateObject.Joint;
                change.ListTarget = PEPlugin.Pmd.UpdateObject.Joint;

                int i = PmxRef.ResolveJoint(pmx, args);
                string name = pmx.Joint[i].Name;
                pmx.Joint.RemoveAt(i);
                return Json.Obj("deleted", name, "index", i, "remaining", pmx.Joint.Count);
            });
        }

        // ---- soft bodies ------------------------------------------------------------

        /// <summary>The PMX 2.1 coefficient names, so they can be read and written in bulk.</summary>
        private static readonly string[] FloatCoefficients =
        {
            "VCF", "DP", "DG", "LF", "PR", "VC", "DF", "MT", "CHR", "KHR", "SHR", "AHR",
            "SRHR_CL", "SKHR_CL", "SSHR_CL", "SR_SPLT_CL", "SK_SPLT_CL", "SS_SPLT_CL",
            "LST", "AST", "VST"
        };

        private static readonly string[] IntCoefficients = { "V_IT", "P_IT", "D_IT", "C_IT" };

        private static object SoftRow(IPXPmx pmx, int i, IPXSoftBody s, bool full)
        {
            Dictionary<string, object> row = Json.Obj(
                "index", i,
                "name", s.Name,
                "nameEn", s.NameE,
                "shape", s.Shape.ToString(),
                "materialIndex", PmxUtil.IndexOf(pmx.Material, s.Material),
                "materialName", s.Material == null ? null : s.Material.Name,
                "group", s.Group,
                "totalMass", s.TotalMass,
                "anchorCount", s.Anchors == null ? 0 : s.Anchors.Count,
                "pinCount", s.Pins == null ? 0 : s.Pins.Count);
            if (full)
            {
                row["passGroup"] = Bools(s.PassGroup);
                row["margin"] = s.Margin;
                row["aeroModel"] = s.AeroModel;
                row["clusterCount"] = s.ClusterCount;
                row["bendingLinkDistance"] = s.BendingLinkDistance;
                row["generateBendingLinks"] = s.GenerateBendingLinks;
                row["generateClusters"] = s.GenerateClusters;
                row["randomizeConstraints"] = s.RandomizeConstraints;

                Dictionary<string, object> co = new Dictionary<string, object>();
                foreach (string n in FloatCoefficients) co[n] = Member(s, n);
                foreach (string n in IntCoefficients) co[n] = Member(s, n);
                row["coefficients"] = co;
            }
            return row;
        }

        /// <summary>
        /// Reads one coefficient by name.  There are 25 of them with no common accessor,
        /// so reflection keeps this from being 25 near-identical lines twice over.
        /// </summary>
        private static object Member(IPXSoftBody s, string name)
        {
            System.Reflection.PropertyInfo p = typeof(IPXSoftBody).GetProperty(name);
            if (p == null || !p.CanRead) return null;
            return p.GetValue(s, null);
        }

        private static void SetMember(IPXSoftBody s, string name, object value)
        {
            System.Reflection.PropertyInfo p = typeof(IPXSoftBody).GetProperty(name);
            if (p == null || !p.CanWrite) return;
            if (p.PropertyType == typeof(int)) p.SetValue(s, Convert.ToInt32(value), null);
            else p.SetValue(s, Convert.ToSingle(value), null);
        }

        private static object ListSoftBodies(Editor editor, Dictionary<string, object> args)
        {
            return editor.Read<object>(delegate(IPXPmx pmx)
            {
                int offset, limit;
                PmxUtil.Page(args, pmx.SoftBody.Count, 200, out offset, out limit);

                List<object> rows = new List<object>();
                for (int i = offset; i < offset + limit; i++)
                {
                    rows.Add(SoftRow(pmx, i, pmx.SoftBody[i], false));
                }
                return Json.Obj(
                    "total", pmx.SoftBody.Count,
                    "offset", offset,
                    "count", rows.Count,
                    "softBodies", rows.ToArray());
            });
        }

        private static object GetSoftBody(Editor editor, Dictionary<string, object> args)
        {
            return editor.Read<object>(delegate(IPXPmx pmx)
            {
                int i = PmxRef.ResolveSoftBody(pmx, args);
                return SoftRow(pmx, i, pmx.SoftBody[i], true);
            });
        }

        private static object SetSoftBody(Editor editor, Dictionary<string, object> args)
        {
            return editor.Edit<object>(delegate(IPXPmx pmx, Editor.Change change)
            {
                change.Target = PmxUpdateObject.SoftBody;
                change.ListTarget = PEPlugin.Pmd.UpdateObject.All;

                int i = PmxRef.ResolveSoftBody(pmx, args);
                IPXSoftBody s = pmx.SoftBody[i];

                if (Json.Has(args, "new_name")) s.Name = Json.Str(args, "new_name", s.Name);
                if (Json.Has(args, "new_name_en")) s.NameE = Json.Str(args, "new_name_en", s.NameE);
                s.Shape = PmxRef.EnumArg(args, "shape", s.Shape);

                bool given;
                IPXMaterial m = PmxRef.MaterialArg(pmx, args, "material", out given);
                if (given) s.Material = m;

                if (Json.Has(args, "group")) s.Group = Json.Int(args, "group", s.Group);
                if (Json.Has(args, "total_mass")) s.TotalMass = Json.Flt(args, "total_mass", s.TotalMass);
                if (Json.Has(args, "margin")) s.Margin = Json.Flt(args, "margin", s.Margin);
                if (Json.Has(args, "aero_model")) s.AeroModel = Json.Int(args, "aero_model", s.AeroModel);
                if (Json.Has(args, "cluster_count")) s.ClusterCount = Json.Int(args, "cluster_count", s.ClusterCount);
                if (Json.Has(args, "bending_link_distance"))
                    s.BendingLinkDistance = Json.Int(args, "bending_link_distance", s.BendingLinkDistance);
                if (Json.Has(args, "generate_bending_links"))
                    s.GenerateBendingLinks = Json.Bool(args, "generate_bending_links", s.GenerateBendingLinks);
                if (Json.Has(args, "generate_clusters"))
                    s.GenerateClusters = Json.Bool(args, "generate_clusters", s.GenerateClusters);
                if (Json.Has(args, "randomize_constraints"))
                    s.RandomizeConstraints = Json.Bool(args, "randomize_constraints", s.RandomizeConstraints);

                Dictionary<string, object> co = Json.Sub(args, "coefficients");
                if (co != null)
                {
                    foreach (KeyValuePair<string, object> kv in co)
                    {
                        SetMember(s, kv.Key, kv.Value);
                    }
                }

                change.Index = i;
                return SoftRow(pmx, i, s, true);
            });
        }
    }
}
