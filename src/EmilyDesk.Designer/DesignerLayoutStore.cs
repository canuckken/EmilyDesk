using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace EmilyDesk.Designer
{
    internal static class DesignerLayoutStore
    {
        public static DesignerLayout Load(string path)
        {
            var serializer = new JavaScriptSerializer();
            return serializer.Deserialize<DesignerLayout>(File.ReadAllText(path));
        }

        public static void Save(string path, DesignerLayout layout)
        {
            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            var serializer = new JavaScriptSerializer();
            string temporary = path + ".saving";
            File.WriteAllText(temporary, serializer.Serialize(layout), Encoding.UTF8);
            if (File.Exists(path))
                File.Replace(temporary, path, null, true);
            else
                File.Move(temporary, path);
        }

        public static string PublishLive(string fileName, DesignerLayout layout, string exportPath)
        {
            if (!string.IsNullOrEmpty(exportPath)) Save(exportPath, layout);
            string livePath = XWidgetReborn.WidgetSdk.DesignerLayoutFiles.LocalPath(fileName);
            Save(livePath, layout);
            XWidgetReborn.WidgetSdk.DesignerLayoutFiles.Reload();
            return livePath;
        }
    }
}
