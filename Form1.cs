using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AForge.Video;
using AForge.Video.DirectShow;
using GMap.NET.MapProviders;
using GMap.NET;

using System.Threading;
using System.Timers;
using System.Diagnostics;
using GMap.NET.WindowsForms.Markers;
using GMap.NET.WindowsForms.ToolTips;
using System.Collections;
using System.IO.Ports;
using System.Media;
using System.Drawing.Imaging;

//web request imports
using System.Net;
using System.Collections.Specialized;
using Newtonsoft.Json;
using System.Net.Http;


// Firebase import dependencies
using FireSharp.Config;
using FireSharp.Interfaces;
using FireSharp.Response;
using System.Diagnostics.Tracing;
using System.Text.Json;
using FireSharp.Extensions;

// speech recognition imports
using System.Speech;
using System.Speech.Synthesis;
using System.Speech.Recognition;
using Timer = System.Timers.Timer;
using Windows.Devices.Geolocation;


// imports for GPS location acquiring and querying
using System.Device.Location;
using GMap.NET.WindowsForms;
using Windows.Foundation;
using Windows.UI.Composition;
using System.Security.Policy;
using Blimp_GCS;
using Windows.Web.Http;
using HttpClient = System.Net.Http.HttpClient;
using HttpResponseMessage = System.Net.Http.HttpResponseMessage;
using System.Security.Cryptography;
using System.Net.Http.Headers;

namespace SearchGCS
{
    public partial class Form1 : Form
    {
        // global counter for mouse operation
        public int mouseCounter;

        bool isOnFire = false; // simulation variables for demo
        Random rng = new Random(); // simulation variables for demo
        SoundPlayer soundPlayer = new SoundPlayer(Blimp_GCS.Properties.Resources.lockon);

        // variable for keeping track of missile left
        int num_missiles = 4;

        // Definition for device GPS

        static GMapProvider[] mapProviders;
        Double LONGITUDE;
        Double LATITUDE;

        String drugType = "";
        Stopwatch stopWatch = new Stopwatch();

        PointLatLng pointBase;
        PointLatLng pointTarget;

        GMapRoute trajectory;
        GMapOverlay routes;
        GMapOverlay markers;
        GMapMarker marker_base;
        GMarkerGoogle plane_marker, plane_marker2;
        GMapOverlay overlayPlane;

        GMapOverlay polygons;
        GMapPolygon polygon;


        GMapMarker marker_target;
        Double distance_to_target;
        GMapOverlay newMarkerOverlay = new GMapOverlay("markers");

        GMarkerGoogle gm_fire;

        // Definition of variables for Radar interface
        Timer t = new Timer(50);
        int WIDTH = 200, HEIGHT = 200, HAND = 100;
        int u; // in degrees
        int cx, cy;     //center of the circle
        int x, y;       //HAND coordinate
        int tx, ty, lim = 20;
        Bitmap bitmap_radar;
        Pen p_radar;
        Pen p_missile;
        Graphics g_radar;
        int numSatellites = 0;


        // define weather rocket
        Blimp_GCS.Missile missile;

        // variable for space plane
        SpacePlane sp, sp2;

        Bitmap marker_fire;


        public Bitmap resized_plane;

        private void MainMap_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                mouseCounter += 1;
                Double lat = MainMap.FromLocalToLatLng(e.X, e.Y).Lat;
                Double lng = MainMap.FromLocalToLatLng(e.X, e.Y).Lng;
                var newMarker = new GMap.NET.WindowsForms.Markers.GMarkerGoogle(
                    new PointLatLng(lat, lng),
                    GMarkerGoogleType.yellow_dot);
                MainMap.Overlays.Add(newMarkerOverlay);
                newMarkerOverlay.Markers.Add(newMarker);
                if (mouseCounter == 4)
                {
                    try
                    {
                        drawPolygon();
                        ss.SpeakAsync("objective changed");
                        updateMap(pointTarget);
                        ss.SpeakAsync("navigating to new area");
                        sp.lockTarget();
                        sp2.lockTarget();
                    }
                    catch (Exception)
                    {

                    }
                }

            }
            // mapclick
        }

        private void MainMap_MouseClick(object sender, MouseEventArgs e)
        {
            
        }

        private void btnVoice_Click(object sender, EventArgs e)
        {
            client = new FireSharp.FirebaseClient(config);

            btnVoice.Enabled = false;
            clist.Add(new string[] { "update satellites", "launch rocket"});
            Grammar gr = new Grammar(new GrammarBuilder(clist));
            try
            {
                sre.RequestRecognizerUpdate();
                sre.LoadGrammar(gr);
                sre.SpeechRecognized += sre_SpeechRecognized;
                sre.SetInputToDefaultAudioDevice();
                sre.RecognizeAsync(RecognizeMode.Multiple);
                ss.SelectVoiceByHints(VoiceGender.Female, VoiceAge.Teen);
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
            }
        }

        private async void sre_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            switch(e.Result.Text.ToString())
            {
                case "update satellites":
                    // making the query to count the number of satellites above me
                    String satResult;
                    SatelliteModel sm = new SatelliteModel
                    {
                        latitude = pointTarget.Lat,
                        longitude = pointTarget.Lng,
                        elevation = 70.0 // by default
                    };
                    var satPayload = await Task.Run(() => JsonConvert.SerializeObject(sm));
                    HttpContent content = new StringContent(satPayload, Encoding.UTF8, "application/json");
                    var url = " YOUR RESTFUL API";


                    using (HttpClient weatherClient = new HttpClient())
                    {
                        using (HttpResponseMessage weatherResponse = await weatherClient.PostAsync(url, content))
                        {
                            using (HttpContent returnedRes = weatherResponse.Content)
                            {
                                satResult = await returnedRes.ReadAsStringAsync();
                            }
                        }
                    }
                    numSatellites = int.Parse(satResult);
                    ss.SpeakAsync($"There are {satResult} operational GPS satellites above you");
                    break;
                case "launch rocket":
                    if (num_missiles > 0)
                    {
                        soundPlayer.Play();
                        num_missiles -= 2;
                        ss.SpeakAsync("missle launched");
                        var tempOverlay = new GMapOverlay("temporary");
                        MainMap.Overlays.Add(tempOverlay);

                        sp.launch();
                        sp2.launch();
                        var missileMarker = sp.getMissileMarker();
                        var missileMarker2 = sp2.getMissileMarker();

                        // missile launch coordinates
                        Double missile_lat = sp.missileLat;
                        Double missile_lng = sp.missileLon;

                        Double missile_lat2 = sp2.missileLat;
                        Double missile_lng2 = sp2.missileLon;

                        for (Double k = 0; k < 1; k += 0.05)
                        {
                            if (k > 0.95)
                            {
                                

                                Bitmap label = Blimp_GCS.Properties.Resources.explosion;
                                Bitmap resized = new Bitmap(label, new Size(50, 50));
                                var newMarker = new GMap.NET.WindowsForms.Markers.GMarkerGoogle(
                                new PointLatLng(pointTarget.Lat, pointTarget.Lng),
                                resized
                                );
                                
                                tempOverlay.Markers.Add(newMarker);

                                // display image for a duration of one second
                                await Task.Delay(1000);
                                soundPlayer.Stop();
                                tempOverlay.Markers.Remove(newMarker);
                            } else
                            {
                                Double missileActualLat = missile_lat + k * (pointTarget.Lat - missile_lat);
                                Double missileActualLon = missile_lng + k * (pointTarget.Lng - missile_lng);

                                Double missileActualLat2 = missile_lat2 + k * (pointTarget.Lat - missile_lat2);
                                Double missileActualLon2 = missile_lng2 + k * (pointTarget.Lng - missile_lng2);

                                missileMarker = sp.updateMissileMarker(missileActualLat, missileActualLon);
                                missileMarker2 = sp2.updateMissileMarker(missileActualLat2, missileActualLon2);

                                try
                                {
                                    tempOverlay.Markers.Add(missileMarker);
                                    tempOverlay.Markers.Add(missileMarker2);
                                    await Task.Delay(250);
                                    tempOverlay.Markers.Remove(missileMarker);
                                    tempOverlay.Markers.Remove(missileMarker2);
                                } catch (Exception)
                                {

                                }  
                            }
                        }
                        
                        sp.unlockTarget();
                        sp2.unlockTarget();
                        polygons.Polygons.Remove(polygon);
                        newMarkerOverlay.Markers.Clear();
                        markers.Markers.Remove(gm_fire);
                        isOnFire = false;
                    }
                    else
                    {
                        ss.SpeakAsync("no medicine left");
                    }
                    break;
                
            }

        }

        private void drawPolygon()
        {
            if (newMarkerOverlay.Markers != null) {
                List<PointLatLng> pointslatlang = new List<PointLatLng>();
                Double sum_lat = 0;
                Double sum_lon = 0;
                
                foreach (GMap.NET.WindowsForms.Markers.GMarkerGoogle m in newMarkerOverlay.Markers)
                {
                    sum_lat += m.Position.Lat;
                    sum_lon += m.Position.Lng;
                    pointslatlang.Add(new PointLatLng(m.Position.Lat, m.Position.Lng));
                }
                sum_lat /= newMarkerOverlay.Markers.Count;
                sum_lon /= newMarkerOverlay.Markers.Count;
                polygon = new GMapPolygon(pointslatlang, "polygon");
                
                polygons.Polygons.Add(polygon);
                pointTarget.Lat = sum_lat;
                pointTarget.Lng = sum_lon;

                //polygons.Markers.Add(marker_target);
                MainMap.Overlays.Add(polygons);


                // refresh the map by changing zoom
                MainMap.Zoom++;
                MainMap.Zoom--;
            }
        }

        private void updateMap(PointLatLng newTarget)
        {
            var route = GoogleMapProvider.Instance.GetRoute(pointBase, newTarget, false, false, 7);
            // remove old routes


            distance_to_target = route.Distance;
            markers.Markers.Add(marker_base);
            MainMap.Overlays.Add(markers);

            //MainMap.Overlays.Add(routes);

            distance_to_target = route.Distance;
            missile = new Blimp_GCS.Missile(pointBase.Lat, pointBase.Lng, 300000, pointTarget, false, distance_to_target, false);
        }


        // initialization for speech recognition
        SpeechSynthesizer ss = new SpeechSynthesizer();
        PromptBuilder pb = new PromptBuilder();
        SpeechRecognitionEngine sre = new SpeechRecognitionEngine();
        Choices clist;

        // Firebase authentication config
        IFirebaseConfig config = new FirebaseConfig
        {
            AuthSecret = "YOUR AUTH SECRET",
            BasePath = "YOUR BASE PATH"
        };

        IFirebaseClient client;

        public Form1()
        {
            InitializeComponent();
        }



        private void Form1_Load(object sender, EventArgs e)
        {
            // initialize the polygons
            polygons = new GMapOverlay("polygons");

            // initialize voice recognition objects
            clist = new Choices();

            // initialization for radar map
            bitmap_radar = new Bitmap(WIDTH + 1, HEIGHT + 1);
            radar_box.BackColor = System.Drawing.Color.Black;
            cx = WIDTH / 2;
            cy = HEIGHT / 2;
            u = 0;

            t.Elapsed += OnTimedEvent;
            t.AutoReset = true;
            t.Enabled = true;


            LATITUDE = 47.2427655;
            LONGITUDE = -122.455138;


            // variables for keeping track of target and base positions
            pointBase = new PointLatLng(LATITUDE, LONGITUDE);
            pointTarget = new PointLatLng(33.0166666, -116.6833306);

            KeyPreview = true;
            Focus();



            #region map_setup
            mapProviders = new GMapProvider[4];
            mapProviders[2] = GMapProviders.BingHybridMap;
            mapProviders[1] = GMapProviders.GoogleHybridMap;
            mapProviders[0] = GMapProviders.GoogleMap;
            mapProviders[3] = GMapProviders.GoogleSatelliteMap;

            GMapProviders.GoogleMap.ApiKey = "your GMAP API";

            for (int i = 0; i < 4; i++)
            {
                cbMapProviders.Items.Add(mapProviders[i]);
            }

            MainMap.DragButton = MouseButtons.Right;
            


            MainMap.MinZoom = 1;
            MainMap.MaxZoom = 20;
            MainMap.CacheLocation = Path.GetDirectoryName(Application.ExecutablePath) + "/mapcache/";

            MainMap.MapProvider = GMap.NET.MapProviders.GoogleMapProvider.Instance;
            MainMap.DragButton = MouseButtons.Left;

            GMaps.Instance.Mode = GMap.NET.AccessMode.ServerAndCache;
            MainMap.Position = new PointLatLng(LATITUDE, LONGITUDE);
            MainMap.MarkersEnabled = true;
            MainMap.ShowCenter = false;


            overlayPlane = new GMapOverlay("plane");

            MainMap.Overlays.Add(overlayPlane);


            // add plane marker to the overlay on map

            
            
            

            markers = new GMapOverlay("markers");
            Bitmap stationLabel = Blimp_GCS.Properties.Resources.AirTower;
            Bitmap baseLabel = new Bitmap(stationLabel, new Size(90, 90));

            marker_base =
                new GMarkerGoogle(
                    pointBase,
                    baseLabel);

            using (var fire_img = Blimp_GCS.Properties.Resources.SOS)
            {
                marker_fire = new Bitmap(fire_img, new Size(50, 40));
            }

            
            
            routes = new GMapOverlay("routes");

            MainMap.Overlays.Add(routes);

            marker_target =
                new GMarkerGoogle(
                    pointTarget,
                    GMarkerGoogleType.red_pushpin);

            var route = GoogleMapProvider.Instance.GetRoute(pointBase, pointTarget, false, false, 7);


            trajectory = new GMapRoute(route.Points, "trajectory")
            {
                Stroke = new Pen(System.Drawing.Color.Transparent, 2)
            };


            routes.Routes.Add(trajectory);


            //markers.Markers.Add(marker_target);
            markers.Markers.Add(marker_base);
            MainMap.Overlays.Add(markers);

            

            MainMap.Zoom = 4;



            // plane will take off from base

            sp = new SpacePlane(pointBase.Lat, pointBase.Lng, 20, 0);
            sp2 = new SpacePlane(pointBase.Lat + 5, pointBase.Lng + 5, 20, 0);

            sp.unlockTarget();
            sp2.unlockTarget();

            plane_marker = sp.getMarker();
            overlayPlane.Markers.Add(plane_marker);


            Timer t_plane = new Timer();
            t_plane.Interval = 200; // update the plane position every 200 ms
            t_plane.Elapsed += (sender_plane, e_plane) => plane_update(sender_plane, e_plane);

            t_plane.AutoReset = true;
            t_plane.Enabled = true;

            #endregion
            
        }

        // updates UAV's position on the map every 200 ms
        private void plane_update(object sender, ElapsedEventArgs e)
        {
            if (!isOnFire)
            {
                if (rng.Next(0, 100) > 80)
                {
                    isOnFire = true;
                    gm_fire = new GMarkerGoogle(
                        new PointLatLng(36.0, sp.longitude + 20),
                        marker_fire
                        );
                    markers.Markers.Add(gm_fire);
                    
                }
            }
            
            try
            {
                overlayPlane.Markers.Clear();
            } catch (Exception)
            {

            }
            sp.updatePath(pointTarget.Lat, pointTarget.Lng);
            sp2.updatePath(pointTarget.Lat, pointTarget.Lng);
            plane_marker = sp.getMarker();
            plane_marker2 = sp2.getMarker();
            overlayPlane.Markers.Add(plane_marker);
            overlayPlane.Markers.Add(plane_marker2);

            if (sp.TargetReached(pointTarget.Lat, pointTarget.Lng) && sp.target_locked) {
                sp.unlockTarget();
                ss.SpeakAsync("medkit dropped");
            }

            if (sp2.TargetReached(pointTarget.Lat, pointTarget.Lng) && sp2.target_locked) {
                sp2.unlockTarget();
                ss.SpeakAsync("medkit dropped");
                    
            }

        }

        

        // drawing method for radar interface
        private void OnTimedEvent(object sender, ElapsedEventArgs e)
        {
            //pen
            if (sp != null && sp.target_locked)
            {
                p_radar = new Pen(System.Drawing.Color.Red, 1f);
            } else
            {
                p_radar = new Pen(System.Drawing.Color.Green, 1f);
            }
            
            
            p_missile = new Pen(System.Drawing.Color.BlueViolet, 1f);

            //graphics
            try
            {
                g_radar = Graphics.FromImage(bitmap_radar);
            } catch
            {

            }
            
            //calculate x, y coordinate of HAND
            int tu = (u - lim) % 360;
            if (u >= 0 && u <= 180)
            {
                //right half
                //u in degree is converted into radian.

                x = cx + (int)(HAND * Math.Sin(Math.PI * u / 180));
                y = cy - (int)(HAND * Math.Cos(Math.PI * u / 180));
            }
            else
            {
                x = cx - (int)(HAND * -Math.Sin(Math.PI * u / 180));
                y = cy - (int)(HAND * Math.Cos(Math.PI * u / 180));
            }

            if (tu >= 0 && tu <= 180)
            {
                //right half
                //tu in degree is converted into radian.

                tx = cx + (int)(HAND * Math.Sin(Math.PI * tu / 180));
                ty = cy - (int)(HAND * Math.Cos(Math.PI * tu / 180));
            }
            else
            {
                tx = cx - (int)(HAND * -Math.Sin(Math.PI * tu / 180));
                ty = cy - (int)(HAND * Math.Cos(Math.PI * tu / 180));
            }

            try
            {
                //draw circle
                g_radar.DrawEllipse(p_radar, 0, 0, WIDTH, HEIGHT);
                g_radar.DrawEllipse(p_radar, 80, 80, WIDTH - 160, HEIGHT - 160);

                //draw perpendicular line
                g_radar.DrawLine(p_radar, new Point(cx, 0), new Point(cx, HEIGHT)); // UP-DOWN
                g_radar.DrawLine(p_radar, new Point(0, cy), new Point(WIDTH, cy)); //LEFT-RIGHT

                //draw HAND
                g_radar.DrawLine(new Pen(System.Drawing.Color.Black, 1f), new Point(cx, cy), new Point(tx, ty));
                g_radar.DrawLine(p_radar, new Point(cx, cy), new Point(x, y));
            } catch(Exception)
            {

            }
            

            // draw satellites
            if (numSatellites != 0) {
                for (int i = 0; i < numSatellites; i++)
                {
                    Bitmap satelliteImg = Blimp_GCS.Properties.Resources.satellite;
                    Bitmap resizedSat = new Bitmap(satelliteImg, new Size(20, 20));
                    g_radar.DrawImage(resizedSat, 20 * i, 10);
                }
            }

            Font missile_font = new System.Drawing.Font("Arial", 6, FontStyle.Regular, GraphicsUnit.Point);
            RectangleF rect_missile = new RectangleF(50, 180, 60, 40);

            // draw missile label
            if (num_missiles != 0)
            {
                try
                {
                    g_radar.DrawString($"Rocket  {num_missiles}", missile_font, Brushes.MediumPurple, rect_missile);
                } catch(Exception)
                {
                    
                }
            }


            radar_box.Image = bitmap_radar;

            //update
            u++;
            if (u == 360)
            {
                u = 0;
            }
        }




        private async void upload_firebase(Bitmap bitmap)
        {
            MemoryStream ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Jpeg);

            byte[] a = ms.GetBuffer();

            string output = Convert.ToBase64String(a);

            var data = new Image_Model
            {
                Img = output
            };

            FirebaseResponse response = await client.UpdateTaskAsync("Image/", data);
        }

        private async void get_firebase(Bitmap bitmap)
        {
            FirebaseResponse response = await client.GetTaskAsync("UserQuery/User1");
            Category res = response.ResultAs<Category>();
            pointTarget.Lat = res.latitude;
            pointTarget.Lng = res.longitude;
            drugType = res.medicine;


        }



        private String UnicodeString(String text)
        {
            return text;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            
        }



        private void cbo_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true;
        }


        private void cbMapProviders_SelectedIndexChanged(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            MainMap.MapProvider = (GMapProvider)cbMapProviders.SelectedItem;
            MainMap.MaxZoom = 19;
            MainMap.Invalidate();

            Cursor = Cursors.Default;
        }

        private void Form1_KeyPress(object sender, KeyPressEventArgs e)
        {

        }



        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            
        }

        private void button3_Click(object sender, EventArgs e)
        {
            client = new FireSharp.FirebaseClient(config);
        }

    }
}
