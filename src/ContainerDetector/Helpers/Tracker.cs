using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

namespace ContainerDetector.Helpers
{
    public class Tracker
    {
        public List<RegionData> Regions { get; set; }
        readonly TimeSpan TimeLimit = new TimeSpan(0, 0, 5);
        const int DistanceLimit = 300; // in pixels
        public List<TrackedObject> Objects { get; set; }

        public Point[] Area { get; set; }

        private BoundingBox observationarea;
        public BoundingBox ObservationArea
        {
            get
            {
                return observationarea;
            }
            set
            {
                observationarea = value;
                Area = new Point[] { new Point(value.Left,value.Top), new Point(value.Left+value.Width, value.Top),
                new Point(value.Left+value.Width,value.Top+value.Height),new Point(value.Left,value.Top+value.Height)
                };
            }
        }
        public int MaxTrackedObject { get; set; }
        public Tracker(BoundingBox Area, int MaxObject = 10)
        {
            this.ObservationArea = Area;
            this.MaxTrackedObject = MaxObject;
            this.Objects = new List<TrackedObject>();
            this.Regions = new List<RegionData>();
        }

        public void AddRegion(BoundingBox box)
        {
            var newRegion = new RegionData();
            newRegion.ID = Regions.Count;
            newRegion.Area = RegionData.CreateFromBox(box);
            Regions.Add(newRegion);
        }

        /// <summary>
        /// counter = how many object change to other region
        /// </summary>
        /// <param name="Output"></param>
        /// <returns></returns>
        public (bool changes, int counter) DoTracking(IList<PredictionModel> Output)
        {
            int changes = 0;
            //resize 
            for (int i = 0; i < Output.Count; i++)
            {
                var item = Output[i];
                item.BoundingBox = new BoundingBox(item.BoundingBox.Left * ObservationArea.Width, item.BoundingBox.Top * ObservationArea.Height, item.BoundingBox.Width * ObservationArea.Width, item.BoundingBox.Height * ObservationArea.Height);
            }
            //update object trail if any
            changes = UpdateTrails(ref Output);

            //add new object if in area observation
            foreach (var item in Output)
            {
                var newObj = new TrackedObject(item.TagName);
                newObj.UpdateTrail(item.BoundingBox);
                if (Geometry.IsPointInPolygon(Area, newObj.Center))
                {
                    Objects.Add(newObj);
                    changes++;
                }
            }
            

            //remove object if it's outside or not updated for a long time
            var removed = new List<TrackedObject>();

            foreach (var item in Objects)
            {
                //based on last update
                var ts = DateTime.Now - item.LastUpdate;
                if (ts > TimeLimit)
                {
                    removed.Add(item);
                } //if it's outside observation area
                else if (!Geometry.IsPointInPolygon(Area, item.Center))
                {
                    removed.Add(item);
                }
            }
            foreach (var item in removed)
            {
                Objects.Remove(item);
            }
            //assign region and maybe counting object
            int counter = 0;
            foreach (var item in Objects)
            {
                //check all object
                foreach(var region in Regions)
                {
                    //if item is inside region
                    if (Geometry.IsPointInPolygon(region.Area, item.Center))
                    {
                        //if the old region is not same with new region
                        if(item.RegionID!=RegionData.DefaultRegionID && item.RegionID != region.ID)
                        {
                            counter++;
                        }
                        item.RegionID = region.ID;
                        //one object can only contain in one region
                        continue;
                    }
                }
            }
            //Debug.WriteLine("tracked obj : "+Objects.Count);
            var res = changes > 0;
            return (res,counter);
        }
        int UpdateTrails(ref IList<PredictionModel> Output)
        {
            var Updated = new List<PredictionModel>();
            foreach (var item in Objects)
            {
                foreach (var output1 in Output)
                {
                    //make sure one object -> one updated trail
                    if (!Updated.Contains(output1))
                    {
                        //if it is close to existing object then update
                        if (Geometry.Euclidean(item.Center, GetCenterFromBox(output1.BoundingBox)) <= DistanceLimit)
                        {
                            item.UpdateTrail(output1.BoundingBox);
                            Updated.Add(output1);
                        }
                    }
                }
            }
            int changes = Updated.Count;
            //remove all updated item, and it will be assigned as new object
            foreach (var item in Updated)
            {
                Output.Remove(item);
            }
            return changes;
        }

        Point GetCenterFromBox(BoundingBox box) => new Point(box.Left + box.Width / 2, box.Top + box.Height / 2);

    }

    public class TrackedObject
    {
        const int MaxTrail = 200;
        public DateTime LastUpdate { get; set; }
        public int RegionID { get; set; } = RegionData.DefaultRegionID;
        public string Tag { get; set; }
        public List<Point> Trails;
        public double Direction { get; set; }
        public Point Center { get; set; }
        BoundingBox area;
        public BoundingBox Area
        {
            get
            {
                return area;
            }
            set
            {
                area = value;
                Center = new Point(area.Left + area.Width / 2, area.Top + area.Height / 2);
            }
        }
        public TrackedObject(string TagName)
        {
            this.Tag = TagName;
            Trails = new List<Point>();

        }
        public void UpdateTrail(BoundingBox newPosition)
        {
            this.LastUpdate = DateTime.Now;
            this.Area = newPosition;
            Trails.Add(this.Center);
            if (Trails.Count > 1)
            {
                Direction = Geometry.GetAngle(Trails[Trails.Count - 2], Trails[Trails.Count - 1]);
            }
            if (Trails.Count > MaxTrail)
            {
                Trails.RemoveAt(0);
            }
        }

    }

    public class RegionData
    {
        public const int DefaultRegionID = -999;
        public Point[] Area { get; set; }
        public int ID { get; set; }

        public static Point[] CreateFromBox(BoundingBox value)
        {
            var points = new Point[] { new Point(value.Left,value.Top), new Point(value.Left+value.Width, value.Top),
                new Point(value.Left+value.Width,value.Top+value.Height),new Point(value.Left,value.Top+value.Height)
                };
            return points;
        }
    }
}
