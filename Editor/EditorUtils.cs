using UnityEngine.Networking;
using System.Threading.Tasks;
namespace BundleSystem
{
    public class EditorUtil
    {
        public static bool PostRequest(string path, string json, out string response, (string, string)[] header = null)
        {
            using (UnityWebRequest www = UnityWebRequest.Put(path, System.Text.Encoding.UTF8.GetBytes(json)))
            {
                www.method = "POST";
                www.SetRequestHeader("Content-Type", "application/json");
                AddHeaders(www, header);
                www.SendWebRequest();

                while (www.responseCode < 200)
                {
                    Task.Delay(100).Wait();
                }

                if (www.responseCode != 200)
                {
                    response = string.Empty;
                    return false;
                }
                else
                {
                    response = www.downloadHandler.text;
                    return true;
                }
            }
        }
        public static bool PutRequest(string path, byte[] file, out string response, (string, string)[] header = null)
        {
            using (UnityWebRequest www = UnityWebRequest.Put(path, file))
            {
                www.SetRequestHeader("Content-Type", "application/json");
                AddHeaders(www, header);
                www.SendWebRequest();

                while (www.responseCode < 200)
                {
                    Task.Delay(100).Wait();
                }

                if (www.responseCode != 200)
                {
                    response = string.Empty;
                    return false;
                }
                else
                {
                    response = www.downloadHandler.text;
                    return true;
                }
            }
        }

        public static bool PutRequest(string path, string json, out string response, (string, string)[] header = null)
        {
            using (UnityWebRequest www = UnityWebRequest.Put(path, System.Text.Encoding.UTF8.GetBytes(json)))
            {
                www.SetRequestHeader("Content-Type", "application/json");
                AddHeaders(www, header);
                www.SendWebRequest();

                while (www.responseCode < 200)
                {
                    Task.Delay(100).Wait();
                }

                if (www.responseCode != 200)
                {
                    response = string.Empty;
                    return false;
                }
                else
                {
                    response = www.downloadHandler.text;
                    return true;
                }
            }
        }
        public static bool GetRequest(string path, out string response, (string, string)[] header = null)
        {
            using (UnityWebRequest www = UnityWebRequest.Get(path))
            {
                www.SetRequestHeader("Content-Type", "application/json");
                AddHeaders(www, header);
                www.SendWebRequest();

                while (www.responseCode < 200)
                {
                    Task.Delay(100).Wait();
                }

                if (www.responseCode != 200)
                {
                    response = string.Empty;
                    return false;
                }
                else
                {
                    response = www.downloadHandler.text;
                    return true;
                }
            }
        }
        private static void AddHeaders(UnityWebRequest www, (string, string)[] header)
        {
            if (header != null)
            {
                foreach (var tuple in header)
                {
                    www.SetRequestHeader(tuple.Item1, tuple.Item2);
                }
            }
        }

    }
}