using System.Collections.Generic;
using PEPlugin.Pmx;

namespace PmxMcp
{
    /// <summary>Model level information and the editor undo stack.</summary>
    internal static class ModelTools
    {
        public static void Register(ToolRegistry registry, Editor editor)
        {
            registry.Add(
                "get_model_info",
                "PMX Editor 中当前模型的概要：名称、注释、各元素数量、文件路径和撤销深度。",
                Schema.None(),
                true,
                delegate(Dictionary<string, object> args) { return GetModelInfo(editor); });

            registry.Add(
                "set_model_info",
                "重命名模型或替换其注释。只修改传入的字段。",
                Schema.Object(Json.Obj(
                    "name", Schema.Str("日文模型名"),
                    "name_en", Schema.Str("英文模型名"),
                    "comment", Schema.Str("日文注释"),
                    "comment_en", Schema.Str("英文注释"))),
                false,
                delegate(Dictionary<string, object> args) { return SetModelInfo(editor, args); });

            registry.Add(
                "get_header",
                "PMX 文件头：格式版本、字符串存储方式和每个顶点携带的追加 UV 通道数。",
                Schema.None(),
                true,
                delegate(Dictionary<string, object> args) { return GetHeader(editor); });

            registry.Add(
                "set_header",
                "编辑 PMX 文件头。改动会在编辑器中生效，但保存时 PMX Editor 会根据模型实际内容写入文件头："
                    + "不使用 2.1 特性的模型无论版本号设为多少都会写成 2.0，"
                    + "没有顶点使用的追加 UV 通道会被丢弃。先设置内容，文件头会随之更新。",
                Schema.Object(Json.Obj(
                    "version", Schema.Num("格式版本，通常为 2.0 或 2.1"),
                    "string_encode", Schema.Int("0 表示字符串以 UTF-16 存储，1 表示 UTF-8"),
                    "uva_count", Schema.Int("每个顶点的追加 UV 通道数，0-4"))),
                false,
                delegate(Dictionary<string, object> args) { return SetHeader(editor, args); });

            registry.Add(
                "undo",
                "撤销 PMX Editor 中的上一次编辑。",
                Schema.None(),
                false,
                delegate(Dictionary<string, object> args) { return Undo(editor); });

            registry.Add(
                "redo",
                "重做 PMX Editor 中上一次撤销的编辑。",
                Schema.None(),
                false,
                delegate(Dictionary<string, object> args) { return Redo(editor); });
        }

        private static object HeaderRow(IPXPmx pmx)
        {
            return Json.Obj(
                "version", pmx.Header.Version,
                "stringEncode", pmx.Header.StringEncode,
                "stringEncodeName", pmx.Header.StringEncode == 0 ? "UTF-16" : "UTF-8",
                "uvaCount", pmx.Header.UVACount);
        }

        private static object GetHeader(Editor editor)
        {
            return editor.Read<object>(delegate(IPXPmx pmx) { return HeaderRow(pmx); });
        }

        private static object SetHeader(Editor editor, Dictionary<string, object> args)
        {
            return editor.Edit<object>(delegate(IPXPmx pmx, Editor.Change change)
            {
                change.Target = PmxUpdateObject.Header;
                change.ListTarget = PEPlugin.Pmd.UpdateObject.Header;

                if (Json.Has(args, "version"))
                {
                    pmx.Header.Version = Json.Flt(args, "version", pmx.Header.Version);
                }
                if (Json.Has(args, "string_encode"))
                {
                    int e = Json.Int(args, "string_encode", pmx.Header.StringEncode);
                    if (e != 0 && e != 1) throw new McpToolException("string_encode must be 0 or 1");
                    pmx.Header.StringEncode = e;
                }
                if (Json.Has(args, "uva_count"))
                {
                    int n = Json.Int(args, "uva_count", pmx.Header.UVACount);
                    if (n < 0 || n > 4) throw new McpToolException("uva_count must be 0 to 4");
                    pmx.Header.UVACount = n;
                }
                return HeaderRow(pmx);
            });
        }

        private static object GetModelInfo(Editor editor)
        {
            return editor.Ui<object>(delegate
            {
                IPXPmx pmx = editor.State();
                return Json.Obj(
                    "filePath", pmx.FilePath,
                    "name", pmx.ModelInfo.ModelName,
                    "nameEn", pmx.ModelInfo.ModelNameE,
                    "comment", pmx.ModelInfo.Comment,
                    "commentEn", pmx.ModelInfo.CommentE,
                    "pmxVersion", pmx.Header.Version,
                    "counts", Json.Obj(
                        "vertex", pmx.Vertex.Count,
                        "material", pmx.Material.Count,
                        "bone", pmx.Bone.Count,
                        "morph", pmx.Morph.Count,
                        "node", pmx.Node.Count,
                        "rigidBody", pmx.Body.Count,
                        "joint", pmx.Joint.Count,
                        "softBody", pmx.SoftBody.Count),
                    "undoCount", editor.Connector.Form.UndoCount,
                    "redoCount", editor.Connector.Form.RedoCount);
            });
        }

        private static object SetModelInfo(Editor editor, Dictionary<string, object> args)
        {
            if (!Json.Has(args, "name") && !Json.Has(args, "name_en")
                && !Json.Has(args, "comment") && !Json.Has(args, "comment_en"))
            {
                throw new McpToolException("Pass at least one of: name, name_en, comment, comment_en.");
            }

            return editor.Edit<object>(delegate(IPXPmx pmx, Editor.Change change)
            {
                change.Target = PmxUpdateObject.ModelInfo;
                change.ListTarget = PEPlugin.Pmd.UpdateObject.Header;
                change.RefreshModel = false;

                if (Json.Has(args, "name")) pmx.ModelInfo.ModelName = Json.Str(args, "name", "");
                if (Json.Has(args, "name_en")) pmx.ModelInfo.ModelNameE = Json.Str(args, "name_en", "");
                if (Json.Has(args, "comment")) pmx.ModelInfo.Comment = Json.Str(args, "comment", "");
                if (Json.Has(args, "comment_en")) pmx.ModelInfo.CommentE = Json.Str(args, "comment_en", "");

                return Json.Obj(
                    "name", pmx.ModelInfo.ModelName,
                    "nameEn", pmx.ModelInfo.ModelNameE,
                    "comment", pmx.ModelInfo.Comment,
                    "commentEn", pmx.ModelInfo.CommentE);
            });
        }

        private static object Undo(Editor editor)
        {
            editor.RequireWrite();
            return editor.Ui<object>(delegate
            {
                editor.Connector.Form.Undo();
                return Json.Obj(
                    "undone", true,
                    "undoCount", editor.Connector.Form.UndoCount,
                    "redoCount", editor.Connector.Form.RedoCount);
            });
        }

        private static object Redo(Editor editor)
        {
            editor.RequireWrite();
            return editor.Ui<object>(delegate
            {
                editor.Connector.Form.Redo();
                return Json.Obj(
                    "redone", true,
                    "undoCount", editor.Connector.Form.UndoCount,
                    "redoCount", editor.Connector.Form.RedoCount);
            });
        }
    }
}
