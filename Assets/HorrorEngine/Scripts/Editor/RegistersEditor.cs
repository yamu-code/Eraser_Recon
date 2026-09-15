using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HorrorEngine
{
    public class RegistersEditor : MonoBehaviour, IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            UpdateAllRegisters();
        }

        [MenuItem("Horror Engine/Registers/Update All")]
        public static void UpdateAllRegisters()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(RegisterDatabase).Name);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                RegisterDatabase dataRegister = (RegisterDatabase)AssetDatabase.LoadAssetAtPath(path, typeof(RegisterDatabase));
                dataRegister.UpdateDatabase();
            }
        }
    }
}