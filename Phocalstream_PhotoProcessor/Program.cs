﻿using Microsoft.DeepZoomTools;
using System.Linq;
using Microsoft.Practices.Unity;
using Phocalstream_Service.Data;
using Phocalstream_Service.Service;
using Phocalstream_Shared.Data;
using Phocalstream_Shared.Data.Model.Photo;
using Phocalstream_Shared.Service;
using Phocalstream_Web.Application.Data;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Threading;

namespace Phocalstream_PhotoProcessor
{
    class Program
    {

        private static string _path;
        private static bool _break;

        private static IPhotoService _service;
        private static IUnitOfWork _unit;

        static void Main(string[] args)
        {
            IUnityContainer container = BuildUnityContainer();

            _service = container.Resolve<IPhotoService>();
            _unit = container.Resolve<IUnitOfWork>();

            _path = args[0];

            Thread t = new Thread(new ThreadStart(BeginProcess));
            t.Start();

            Console.WriteLine("Press any key to terminate the import");
            Console.ReadKey();
            _break = true;
        }

        private static void BeginProcess()
        {
            XmlDocument xml = new XmlDocument();
            xml.Load(Path.Combine(_path, @"Phocalstream_Manifest.xml"));

            XmlNodeList siteList = xml.SelectNodes("/SiteList/Site");

            foreach (XmlNode siteNode in siteList)
            {
                string dirName = siteNode["Folder"].InnerText;
                string[] files = Directory.GetFiles(Path.Combine(_path, dirName), "*.JPG", SearchOption.AllDirectories);
                List<string> siteFiles = new List<string>();
                
                using (SqlConnection conn = new SqlConnection(System.Configuration.ConfigurationManager.ConnectionStrings["DbConnection"].ConnectionString))
                {
                    conn.Open();
                    using (SqlCommand command = new SqlCommand("select FileName from Photos inner join CameraSites on CameraSites.ID = Photos.Site_ID where CameraSites.Name = @name", conn))
                    {
                        command.Parameters.AddWithValue("@name", dirName);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                siteFiles.Add(reader.GetString(0));
                            }
                        }
                    }
                }

                IEnumerable<string> toProcess = (from f in files where siteFiles.Contains(f) == false select f);
                if ( toProcess.Count() != 0 )
                {
                    Collection collection = _service.GetCollectionForProcessing(siteNode);
                    CameraSite site = collection.Site;
                    _unit.Commit();

                    Console.WriteLine(string.Format("Processing {0} photos for site {1}", toProcess.Count(), site.Name));

                    foreach (string file in toProcess)
                    {
                        _service.ProcessPhoto(file, site);
                        if ( _break)
                            break;
                    }
                    _unit.Commit();

                    Console.WriteLine(string.Format("Building deep zoom site collection for site {0}", site.Name));
                    _service.ProcessCollection(collection);
                    _unit.Commit();

                    Console.WriteLine(string.Format("Building pivot viewer manifest for site {0}", site.Name));
                    _service.GeneratePivotManifest(site);
                }
            }
            Console.WriteLine("Import process complete");
        }

        private static IUnityContainer BuildUnityContainer()
        {
            var container = new UnityContainer();
            container.RegisterType(typeof(IUnitOfWork), typeof(UnitOfWork));
            container.RegisterType(typeof(IPhotoService), typeof(PhotoService));
            container.RegisterType(typeof(IEntityRepository<>), typeof(EntityRepository<>));

            container.RegisterType(typeof(IPhotoRepository), typeof(PhotoRepository),
                new InjectionConstructor(ConfigurationManager.ConnectionStrings["DbConnection"].ConnectionString));

            container.RegisterType(typeof(DbContext), typeof(ApplicationContext));

            container.RegisterInstance(new ApplicationContextAdapter(container.Resolve<DbContext>()), new HierarchicalLifetimeManager());
            container.RegisterType<IDbSetFactory>(new InjectionFactory(con => con.Resolve<ApplicationContextAdapter>()));
            container.RegisterType<IDbContext>(new InjectionFactory(con => con.Resolve<ApplicationContextAdapter>()));

            return container;
        }
    }
}