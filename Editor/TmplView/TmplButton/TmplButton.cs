using UnityEditor;
using UnityEngine;

namespace UNIArt.Editor
{
    public class TmplButton : SelectableView
    {
        public bool IsBuiltIn => TemplateID == "Standard";

        private string templateID = string.Empty;
        public string TemplateID
        {
            get { return templateID; }
            set
            {
                templateID = value;
                Refresh();
            }
        }

        public bool IsSelected { get; set; } = false;

        public bool IsInstalled { get; protected set; } = false;

        public string RootFolder => UNIArtSettings.GetExternalTemplateFolder(TemplateID);

        public void Refresh()
        {
            IsInstalled = SVNIntegration.HasExternal(
                UNIArtSettings.DefaultSettings.TemplateLocalFolder,
                TemplateID
            );
        }

        public bool Pull()
        {
            if (!IsInstalled)
                return false;
            if (SVNIntegration.IsWorkingCopy(RootFolder))
            {
                if (SVNIntegration.Update(RootFolder))
                {
                    AssetDatabase.Refresh();
                    return true;
                }
                return false;
            }

            if (!AssetDatabase.DeleteAsset(RootFolder))
            {
                Debug.LogWarning("Failed to delete external template folder.");
                return false;
            }

            if (!SVNIntegration.Update(UNIArtSettings.DefaultSettings.TemplateLocalFolder))
            {
                Debug.LogWarning("Failed to checkout external template.");
                return false;
            }
            AssetDatabase.Refresh();
            return true;
        }

        public TmplButton()
        {
            BindView(Utils.PackageAssetPath("Editor/TmplView/TmplButton/TmplButton.uxml"));
        }

        public bool CleanDir()
        {
            return AssetDatabase.DeleteAsset(RootFolder);
        }
    }
}
