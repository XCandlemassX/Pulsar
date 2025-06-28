using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace Pulsar.Server.Models
{
    public class Favorites
    {
        private static readonly string PulsarStuffDir = Path.Combine(Application.StartupPath, "PulsarStuff");
        private static readonly string FavoritesPath = Path.Combine(PulsarStuffDir, "favorites.json");
        private static List<string> _favoriteClients = new List<string>();

        public static void LoadFavorites()
        {
            try
            {
                if (File.Exists(FavoritesPath))
                {
                    var json = File.ReadAllText(FavoritesPath);
                    _favoriteClients = JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
                }
            }
            catch
            {
                _favoriteClients = new List<string>();
            }
        }

        public static void SaveFavorites()
        {
            try
            {
                if (!Directory.Exists(PulsarStuffDir))
                {
                    Directory.CreateDirectory(PulsarStuffDir);
                }
                var json = JsonConvert.SerializeObject(_favoriteClients, Formatting.Indented);
                File.WriteAllText(FavoritesPath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        public static bool IsFavorite(string clientId)
        {
            return _favoriteClients.Contains(clientId);
        }

        public static void ToggleFavorite(string clientId)
        {
            if (_favoriteClients.Contains(clientId))
            {
                _favoriteClients.Remove(clientId);
            }
            else
            {
                _favoriteClients.Add(clientId);
            }
            SaveFavorites();
        }
    }
}