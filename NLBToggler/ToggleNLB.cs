using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Web.Script.Serialization;

//
//    The following expample enables the NLB node '12345'
//
//    <UsingTask TaskName="SocialShield.NLBToggler.ToggleNLB"
//        AssemblyFile="NLBToggler.dll" /> 
//
//    <ToggleNLB
//        APIUserName="softlayer user"
//        APIKey="123123123123123123123123"
//        NlbRecordId="12345"
//        Enable="true"
//    />
//

namespace SocialShield.NLBToggler
{
    public class ToggleNLB : ITask
    {
        private const string ApiEndPoint = "https://api.service.softlayer.com/rest/v3/";
        private const string NlbRecordStatusUri = "SoftLayer_Network_Application_Delivery_Controller_LoadBalancer_Service/{0}?objectMask=enabled";
        private const string NlbRecordToggleUri = "SoftLayer_Network_Application_Delivery_Controller_LoadBalancer_Service/{0}/toggleStatus.json";

        public IBuildEngine BuildEngine { get; set; }
        public ITaskHost HostObject { get; set; }
        [Required]
        public string APIUserName { get; set; }
        [Required]
        public string APIKey { get; set; }
        [Required]
        public string NlbRecordId { get; set; }
        [Required]
        public bool Enable { get; set; }


        static ToggleNLB()
        {
            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(ValidateServerCertificate);
        }

        private static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        private string DoSoftLayerAPIRequest(string url)
        {
            string authPair = this.APIUserName + @":" + this.APIKey;
            authPair = System.Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(authPair));

            var request = WebRequest.Create(ApiEndPoint + url);
            request.Headers.Add("Authorization", "Basic " + authPair);
            request.Method = "GET";
            request.ContentType = "text/json";

            using (var response = request.GetResponse())
            {
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private bool ExpectedState()
        {
            var json = DoSoftLayerAPIRequest(string.Format(NlbRecordStatusUri, this.NlbRecordId));
            var response = (new JavaScriptSerializer()).Deserialize<Dictionary<string, object>>(json);

            return ((int)response["enabled"] == 1) == this.Enable;
        }

        private bool Toggle()
        {
            var json = DoSoftLayerAPIRequest(string.Format(NlbRecordToggleUri, this.NlbRecordId));
            var response = (new JavaScriptSerializer()).Deserialize<object>(json);

            return (bool)response;
        }

        public bool Execute()
        {
            try
            {
                if (!ExpectedState())
                {
                    if (!Toggle())
                    {
                        return Error("Could not toggle the NLB record: " + this.NlbRecordId);
                    }
                }
            }
            catch (Exception ex)
            {
                return Error("Exception received while managing the NLB: " + ex.ToString());
            }
            return true;
        }


        private bool Error(string message)
        {
            this.BuildEngine.LogErrorEvent(new BuildErrorEventArgs(String.Empty, string.Empty, string.Empty, 0, 0, 0, 0,
                message, string.Empty, string.Empty)
            );
            return false;
        }
    }
}
