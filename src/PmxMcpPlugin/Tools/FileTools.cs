using System.Collections.Generic;
using System.IO;

namespace PmxMcp
{
    /// <summary>Opening and saving model files through the editor.</summary>
    internal static class FileTools
    {
        public static void Register(ToolRegistry registry, Editor editor)
        {
            registry.Add(
                "open_model",
                "在 PMX Editor 中打开 .pmx 或 .pmd 文件，替换当前已加载的模型。未保存的修改会丢失。",
                Schema.Object(Json.Obj(
                    "path", Schema.Str("要打开的 .pmx 或 .pmd 文件的绝对路径")),
                    "path"),
                false,
                delegate(Dictionary<string, object> args) { return OpenModel(editor, args); });

            registry.Add(
                "save_model",
                "保存当前模型。不传 path 时覆盖已打开的文件；传入 path 时写入新文件，"
                    + "且除非 overwrite 为 true，否则不会覆盖已存在的文件。",
                Schema.Object(Json.Obj(
                    "path", Schema.Str("要写入的 .pmx 文件的绝对路径"),
                    "overwrite", Schema.Bool("允许覆盖 path 处已存在的文件（默认 false）"))),
                false,
                delegate(Dictionary<string, object> args) { return SaveModel(editor, args); });
        }

        private static object OpenModel(Editor editor, Dictionary<string, object> args)
        {
            editor.RequireWrite();
            editor.RequireFileAccess();

            string path = Json.Str(args, "path", "");
            if (string.IsNullOrEmpty(path)) throw new McpToolException("path is required");
            if (!File.Exists(path)) throw new McpToolException("file not found: " + path);

            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension != ".pmx" && extension != ".pmd")
            {
                throw new McpToolException("only .pmx and .pmd files can be opened, got " + extension);
            }

            return editor.Ui<object>(delegate
            {
                if (extension == ".pmx")
                {
                    editor.Connector.Form.OpenPMXFile(path);
                }
                else
                {
                    editor.Connector.Form.OpenPMDFile(path);
                }

                return Json.Obj(
                    "opened", path,
                    "name", editor.State().ModelInfo.ModelName);
            });
        }

        private static object SaveModel(Editor editor, Dictionary<string, object> args)
        {
            editor.RequireWrite();
            editor.RequireFileAccess();

            string path = Json.Str(args, "path", "");
            bool overwrite = Json.Bool(args, "overwrite", false);

            return editor.Ui<object>(delegate
            {
                string target = path;
                if (string.IsNullOrEmpty(target))
                {
                    target = editor.Connector.Pmx.CurrentPath;
                    if (string.IsNullOrEmpty(target))
                    {
                        throw new McpToolException("this model has no file yet; pass an explicit path");
                    }
                }
                else
                {
                    if (Path.GetExtension(target).ToLowerInvariant() != ".pmx")
                    {
                        throw new McpToolException("path must end with .pmx");
                    }
                    if (File.Exists(target) && !overwrite)
                    {
                        throw new McpToolException(target + " already exists; pass overwrite=true to replace it");
                    }
                    string directory = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        throw new McpToolException("directory does not exist: " + directory);
                    }
                }

                editor.Connector.Form.SavePMXFile(target);

                long size = 0;
                if (File.Exists(target)) size = new FileInfo(target).Length;

                return Json.Obj("saved", target, "bytes", size);
            });
        }
    }
}
