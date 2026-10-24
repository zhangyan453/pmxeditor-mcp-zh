using System.Collections.Generic;
using PEPlugin.Form;
using PEPlugin.View;

namespace PmxMcp
{
    /// <summary>What the user has selected in the editor lists and in PmxView.</summary>
    internal static class SelectionTools
    {
        private const int MaxReportedIndices = 500;

        public static void Register(ToolRegistry registry, Editor editor)
        {
            registry.Add(
                "get_selection",
                "读取当前选择：在 PmxView 和编辑器列表中选中的骨骼、顶点、面和材质。"
                    + "过长的索引列表会被截断，完整数量以 count 报告。",
                Schema.None(),
                true,
                delegate(Dictionary<string, object> args) { return GetSelection(editor); });

            registry.Add(
                "set_selection",
                "替换 PmxView 和材质列表中的选择。只需传入想要更改的种类。",
                Schema.Object(Json.Obj(
                    "bone_indices", Schema.IntArray("要选择的骨骼索引"),
                    "vertex_indices", Schema.IntArray("要选择的顶点索引"),
                    "face_indices", Schema.IntArray("要选择的面索引"),
                    "material_indices", Schema.IntArray("要选择的材质索引"))),
                false,
                delegate(Dictionary<string, object> args) { return SetSelection(editor, args); });
        }

        private static object GetSelection(Editor editor)
        {
            return editor.Ui<object>(delegate
            {
                IPXPmxViewConnector view = editor.Connector.View.PmxView;
                IPEFormConnector form = editor.Connector.Form;

                return Json.Obj(
                    "bones", Cap(view.GetSelectedBoneIndices()),
                    "vertices", Cap(view.GetSelectedVertexIndices()),
                    "faces", Cap(view.GetSelectedFaceIndices()),
                    "materials", Cap(form.GetSelectedMaterialIndices()),
                    "listCursor", Json.Obj(
                        "bone", form.SelectedBoneIndex,
                        "material", form.SelectedMaterialIndex,
                        "morph", form.SelectedExpressionIndex,
                        "vertex", form.SelectedVertexIndex,
                        "rigidBody", form.SelectedBodyIndex,
                        "joint", form.SelectedJointIndex));
            });
        }

        private static object SetSelection(Editor editor, Dictionary<string, object> args)
        {
            editor.RequireWrite();

            int[] bones = Json.Ints(args, "bone_indices");
            int[] vertices = Json.Ints(args, "vertex_indices");
            int[] faces = Json.Ints(args, "face_indices");
            int[] materials = Json.Ints(args, "material_indices");

            if (bones == null && vertices == null && faces == null && materials == null)
            {
                throw new McpToolException(
                    "Pass at least one of: bone_indices, vertex_indices, face_indices, material_indices.");
            }

            return editor.Ui<object>(delegate
            {
                IPXPmxViewConnector view = editor.Connector.View.PmxView;
                IPEFormConnector form = editor.Connector.Form;

                if (bones != null) view.SetSelectedBoneIndices(bones);
                if (vertices != null) view.SetSelectedVertexIndices(vertices);
                if (faces != null) view.SetSelectedFaceIndices(faces);
                if (materials != null) form.SetSelectedMaterialIndices(materials);

                view.UpdateView();

                return Json.Obj(
                    "bones", bones == null ? -1 : bones.Length,
                    "vertices", vertices == null ? -1 : vertices.Length,
                    "faces", faces == null ? -1 : faces.Length,
                    "materials", materials == null ? -1 : materials.Length);
            });
        }

        private static Dictionary<string, object> Cap(int[] indices)
        {
            if (indices == null)
            {
                return Json.Obj("count", 0, "indices", new object[0], "truncated", false);
            }

            int take = indices.Length < MaxReportedIndices ? indices.Length : MaxReportedIndices;
            object[] shown = new object[take];
            for (int i = 0; i < take; i++) shown[i] = indices[i];

            return Json.Obj(
                "count", indices.Length,
                "indices", shown,
                "truncated", take < indices.Length);
        }
    }
}
