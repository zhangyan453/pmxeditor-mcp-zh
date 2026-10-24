using System.Collections.Generic;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxMcp
{
    internal static class MaterialTools
    {
        public static void Register(ToolRegistry registry, Editor editor)
        {
            registry.Add(
                "list_materials",
                "列出材质及其索引、名称、面数、颜色和贴图路径。",
                Schema.Object(Json.Obj(
                    "offset", Schema.Int("返回的起始材质索引（默认 0）"),
                    "limit", Schema.Int("返回的材质数量（默认 200，最大 1000）"),
                    "name_contains", Schema.Str("仅返回日文名或英文名包含此文本的材质"))),
                true,
                delegate(Dictionary<string, object> args) { return ListMaterials(editor, args); });

            registry.Add(
                "set_material",
                "编辑单个材质。可用索引或名称指定，只修改传入的字段。"
                    + "颜色分量范围为 0.0-1.0。",
                Schema.Object(Json.Obj(
                    "index", Schema.Int("材质索引"),
                    "name", Schema.Str("用于查找材质的日文名"),
                    "new_name", Schema.Str("新的日文名"),
                    "new_name_en", Schema.Str("新的英文名"),
                    "diffuse", Schema.NumArray("漫反射颜色 [r, g, b, a]", 4),
                    "specular", Schema.NumArray("高光颜色 [r, g, b]", 3),
                    "ambient", Schema.NumArray("环境光颜色 [r, g, b]", 3),
                    "power", Schema.Num("高光强度"),
                    "edge", Schema.Bool("绘制轮廓线"),
                    "edge_color", Schema.NumArray("轮廓线颜色 [r, g, b, a]", 4),
                    "edge_size", Schema.Num("轮廓线粗细"),
                    "both_draw", Schema.Bool("双面渲染"),
                    "texture", Schema.Str("相对于模型的贴图路径"),
                    "sphere", Schema.Str("相对于模型的球面贴图路径"),
                    "sphere_mode", Schema.Str("球面混合模式：" + Spheres),
                    "toon", Schema.Str("卡通贴图路径，或共享名称如 toon01.bmp"),
                    "shadow", Schema.Bool("投射到地面阴影"),
                    "self_shadow_map", Schema.Bool("写入自阴影贴图"),
                    "self_shadow", Schema.Bool("接收自阴影"),
                    "vertex_color", Schema.Bool("使用顶点颜色（PMX 2.1）"),
                    "primitive_type", Schema.Str("图元类型（PMX 2.1）：" + Primitives),
                    "memo", Schema.Str("自由文本备注"))),
                false,
                delegate(Dictionary<string, object> args) { return SetMaterial(editor, args); });

            registry.Add(
                "get_material",
                "查看单个材质的完整信息，包括阴影标志及其面在绘制顺序中的位置。",
                Schema.Object(Json.Obj(
                    "index", Schema.Int("材质索引"),
                    "name", Schema.Str("日文材质名"))),
                true,
                delegate(Dictionary<string, object> args) { return GetMaterial(editor, args); });

            registry.Add(
                "delete_material",
                "删除一个材质及其所属的所有面。顶点会保留；"
                    + "如需一并清除请运行编辑器的清理功能。",
                Schema.Object(Json.Obj(
                    "index", Schema.Int("材质索引"),
                    "name", Schema.Str("日文材质名"))),
                false,
                delegate(Dictionary<string, object> args) { return DeleteMaterial(editor, args); });
        }

        private static readonly string Spheres = PmxRef.Choices(typeof(SphereType));
        private static readonly string Primitives = PmxRef.Choices(typeof(PrimitiveType));

        /// <summary>Every field of one material, shared by get_material and set_material.</summary>
        private static object Detail(IPXPmx pmx, int index)
        {
            IPXMaterial m = pmx.Material[index];

            // The face range is where this material starts in the model's draw order,
            // which is what a renderer needs and PMX Editor does not show directly.
            int start = 0;
            for (int i = 0; i < index; i++)
            {
                start += pmx.Material[i].Faces == null ? 0 : pmx.Material[i].Faces.Count;
            }

            return Json.Obj(
                "index", index,
                "name", m.Name,
                "nameEn", m.NameE,
                "faceCount", m.Faces == null ? 0 : m.Faces.Count,
                "faceStart", start,
                "diffuse", PmxUtil.Vec4(m.Diffuse),
                "specular", PmxUtil.Vec3(m.Specular),
                "ambient", PmxUtil.Vec3(m.Ambient),
                "power", m.Power,
                "edge", m.Edge,
                "edgeColor", PmxUtil.Vec4(m.EdgeColor),
                "edgeSize", m.EdgeSize,
                "bothDraw", m.BothDraw,
                "shadow", m.Shadow,
                "selfShadowMap", m.SelfShadowMap,
                "selfShadow", m.SelfShadow,
                "vertexColor", m.VertexColor,
                "primitiveType", m.PrimitiveType.ToString(),
                "texture", m.Tex,
                "sphere", m.Sphere,
                "sphereMode", m.SphereMode.ToString(),
                "toon", m.Toon,
                "memo", m.Memo);
        }

        private static object GetMaterial(Editor editor, Dictionary<string, object> args)
        {
            return editor.Read<object>(delegate(IPXPmx pmx)
            {
                return Detail(pmx, PmxUtil.ResolveMaterial(pmx, args));
            });
        }

        private static object DeleteMaterial(Editor editor, Dictionary<string, object> args)
        {
            return editor.Edit<object>(delegate(IPXPmx pmx, Editor.Change change)
            {
                change.Target = PmxUpdateObject.Material;
                change.ListTarget = PEPlugin.Pmd.UpdateObject.Material;

                int index = PmxUtil.ResolveMaterial(pmx, args);
                IPXMaterial m = pmx.Material[index];
                int faces = m.Faces == null ? 0 : m.Faces.Count;
                string name = m.Name;
                pmx.Material.RemoveAt(index);
                return Json.Obj("deleted", name, "index", index,
                                "facesRemoved", faces, "remaining", pmx.Material.Count);
            });
        }

        private static object ListMaterials(Editor editor, Dictionary<string, object> args)
        {
            return editor.Read<object>(delegate(IPXPmx pmx)
            {
                string filter = Json.Str(args, "name_contains", null);
                List<int> matched = new List<int>();
                for (int i = 0; i < pmx.Material.Count; i++)
                {
                    IPXMaterial material = pmx.Material[i];
                    if (filter == null || PmxUtil.Matches(material.Name, filter) || PmxUtil.Matches(material.NameE, filter))
                    {
                        matched.Add(i);
                    }
                }

                int offset, limit;
                PmxUtil.Page(args, matched.Count, 200, out offset, out limit);

                List<object> rows = new List<object>();
                for (int n = offset; n < offset + limit; n++)
                {
                    int i = matched[n];
                    IPXMaterial material = pmx.Material[i];
                    rows.Add(Json.Obj(
                        "index", i,
                        "name", material.Name,
                        "nameEn", material.NameE,
                        "faceCount", material.Faces == null ? 0 : material.Faces.Count,
                        "diffuse", PmxUtil.Vec4(material.Diffuse),
                        "specular", PmxUtil.Vec3(material.Specular),
                        "ambient", PmxUtil.Vec3(material.Ambient),
                        "power", material.Power,
                        "edge", material.Edge,
                        "edgeColor", PmxUtil.Vec4(material.EdgeColor),
                        "edgeSize", material.EdgeSize,
                        "bothDraw", material.BothDraw,
                        "texture", material.Tex,
                        "sphere", material.Sphere,
                        "toon", material.Toon,
                        "memo", material.Memo));
                }

                return Json.Obj(
                    "total", pmx.Material.Count,
                    "matched", matched.Count,
                    "offset", offset,
                    "count", rows.Count,
                    "materials", rows.ToArray());
            });
        }

        private static object SetMaterial(Editor editor, Dictionary<string, object> args)
        {
            return editor.Edit<object>(delegate(IPXPmx pmx, Editor.Change change)
            {
                change.Target = PmxUpdateObject.Material;
                change.ListTarget = PEPlugin.Pmd.UpdateObject.Material;

                int index = PmxUtil.ResolveMaterial(pmx, args);
                IPXMaterial material = pmx.Material[index];

                if (Json.Has(args, "new_name")) material.Name = Json.Str(args, "new_name", material.Name);
                if (Json.Has(args, "new_name_en")) material.NameE = Json.Str(args, "new_name_en", material.NameE);
                if (Json.Has(args, "power")) material.Power = Json.Flt(args, "power", material.Power);
                if (Json.Has(args, "edge")) material.Edge = Json.Bool(args, "edge", material.Edge);
                if (Json.Has(args, "edge_size")) material.EdgeSize = Json.Flt(args, "edge_size", material.EdgeSize);
                if (Json.Has(args, "both_draw")) material.BothDraw = Json.Bool(args, "both_draw", material.BothDraw);
                if (Json.Has(args, "texture")) material.Tex = Json.Str(args, "texture", material.Tex);
                if (Json.Has(args, "sphere")) material.Sphere = Json.Str(args, "sphere", material.Sphere);
                if (Json.Has(args, "toon")) material.Toon = Json.Str(args, "toon", material.Toon);
                if (Json.Has(args, "memo")) material.Memo = Json.Str(args, "memo", material.Memo);
                if (Json.Has(args, "shadow")) material.Shadow = Json.Bool(args, "shadow", material.Shadow);
                if (Json.Has(args, "self_shadow_map"))
                    material.SelfShadowMap = Json.Bool(args, "self_shadow_map", material.SelfShadowMap);
                if (Json.Has(args, "self_shadow"))
                    material.SelfShadow = Json.Bool(args, "self_shadow", material.SelfShadow);
                if (Json.Has(args, "vertex_color"))
                    material.VertexColor = Json.Bool(args, "vertex_color", material.VertexColor);
                material.SphereMode = PmxRef.EnumArg(args, "sphere_mode", material.SphereMode);
                material.PrimitiveType = PmxRef.EnumArg(args, "primitive_type", material.PrimitiveType);

                float[] diffuse = Json.Floats(args, "diffuse", 4);
                if (diffuse != null) material.Diffuse = new V4(diffuse[0], diffuse[1], diffuse[2], diffuse[3]);

                float[] specular = Json.Floats(args, "specular", 3);
                if (specular != null) material.Specular = new V3(specular[0], specular[1], specular[2]);

                float[] ambient = Json.Floats(args, "ambient", 3);
                if (ambient != null) material.Ambient = new V3(ambient[0], ambient[1], ambient[2]);

                float[] edgeColor = Json.Floats(args, "edge_color", 4);
                if (edgeColor != null) material.EdgeColor = new V4(edgeColor[0], edgeColor[1], edgeColor[2], edgeColor[3]);

                change.Index = index;
                return Detail(pmx, index);
            });
        }
    }
}
