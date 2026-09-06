using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace uYouWin.Models
{
    internal class InvidiousSettings
    {
        public string BaseUrl { get; set; }

        public bool IsEnabled { get; set; }

        public InvidiousSettings()
        {
            IsEnabled = true;
            BaseUrl = "https://yt.chocolatemoo53.com/"; // Temporary default Invidious instance
        }
    
    }
}
