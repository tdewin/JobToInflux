using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using static System.Environment;

namespace JobToInflux
{

    [DataContract]
    public class InfluxCollectConfig
    {
        [DataMember(IsRequired = true)]
        private string server;
        [DataMember(IsRequired = true)]
        private int port;
        [DataMember(IsRequired = true)]
        private string db;

        public InfluxCollectConfig() : this("127.0.0.1",8086,"defaultdb")
        {
            
        }

        public InfluxCollectConfig(string server,int port,string db)
        {
            this.server = server;
            this.port = port;
            this.db = db;
        }

        public string GetServiceString()
        {
            return String.Format("http://{0}:{1}", this.server, this.port);
        }
        public string GetDB()
        {
            return this.db;
        }

        public static string GetDefaultPath()
        {
            return String.Format("{0}\\InfluxCollect", Environment.GetFolderPath(SpecialFolder.CommonApplicationData));
        }
        public static string GetDefaultConfigFileName()
        {
            return "config.json";
        }

        public static void TryMakeDefaultConfig(string directory,string file)
        {
            if (!Directory.Exists(directory))
            {
                try
                {
                    Directory.CreateDirectory(directory);
                }
                catch (Exception e)
                {
                    throw new Exception(String.Format("Could not make directory {0} although it doesnt exists {1}", directory, e.Message));
                }
            }
            string configfile = String.Format("{0}\\{1}", directory,file) ;

            var influxconfig = new InfluxCollectConfig();
            if (!File.Exists(configfile))
            {
                try
                {
                    influxconfig.WriteToFile(configfile);
                }
                catch (Exception e)
                {
                    throw new Exception(String.Format("Could not write default config file {0} error {1}", configfile, e.Message));
                }

            }

        }

        public static InfluxCollectConfig ReadFromFile(string file)
        {
            var ser = new DataContractJsonSerializer(typeof(InfluxCollectConfig));
            Stream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            InfluxCollectConfig config = (InfluxCollectConfig)ser.ReadObject(stream);
            stream.Close();

            return config;
        }

        public void WriteToFile(string file)
        {
            var ser = new DataContractJsonSerializer(typeof(InfluxCollectConfig));
            Stream stream = File.Open(file, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            ser.WriteObject(stream,this);
            stream.Close();
        }
    }
}
