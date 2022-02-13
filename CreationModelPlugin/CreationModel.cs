using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreationModelPlugin
{
    [TransactionAttribute(TransactionMode.Manual)]
    public class CreationModel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;


            List<Level> levelList = CreateLevelList(doc);
            Level level1 = LevelByName(levelList, "Level 1");
            Level level2 = LevelByName(levelList, "Level 2");

            double width = UnitUtils.ConvertToInternalUnits(10000, UnitTypeId.Millimeters);
            double depth = UnitUtils.ConvertToInternalUnits(5000, UnitTypeId.Millimeters);
            List<XYZ> points = CreateClosedRectangleCurve(width, depth);

            double sillHeight = UnitUtils.ConvertToInternalUnits(900, UnitTypeId.Millimeters);

            Transaction transaction = new Transaction(doc);
            transaction.Start("Create House");

            List<Wall> walls = CreateWalls(doc, points, level1, level2);
            AddDoor(doc, level1, walls[0]);
            for (int i = 1; i < walls.Count; i++)
            {
                AddWindow(doc, level1, walls[i], sillHeight);
            }
            //AddRoof(doc, level2, walls);
            AddExtrusionRoof(doc, level2, walls);

            transaction.Commit();
            return Result.Succeeded;
        }


        public List<Level> CreateLevelList(Document doc)
        {
            List<Level> levelList = new FilteredElementCollector(doc)
                                    .OfClass(typeof(Level))
                                    .OfType<Level>()
                                    .ToList();
            return levelList;
        }

        public Level LevelByName(List<Level> levelList, string name)
        {
            Level levelByName = levelList
                                .Where(x => x.Name.Equals(name))
                                .FirstOrDefault();
            return levelByName;
        }

        public List<XYZ> CreateClosedRectangleCurve(double x, double y)
        {
            List<XYZ> rectangle = new List<XYZ>();
            rectangle.Add(new XYZ(-x / 2, -y / 2, 0));
            rectangle.Add(new XYZ(x / 2, -y / 2, 0));
            rectangle.Add(new XYZ(x / 2, y / 2, 0));
            rectangle.Add(new XYZ(-x / 2, y / 2, 0));
            rectangle.Add(new XYZ(-x / 2, -y / 2, 0));

            return rectangle;
        }

        public List<Wall> CreateWalls(Document doc, List<XYZ> closedCurve, Level baseLevel, Level upperLevel)
        {
            List<Wall> walls = new List<Wall>();
            for (int i = 0; i < closedCurve.Count() - 1; i++)
            {
                Line line = Line.CreateBound(closedCurve[i], closedCurve[i + 1]);
                Wall wall = Wall.Create(doc, line, baseLevel.Id, false);
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(upperLevel.Id);
                walls.Add(wall);
            }
            return walls;
        }

        private void AddDoor(Document doc, Level level, Wall wall)
        {
            FamilySymbol doorType = new FilteredElementCollector(doc)
                                      .OfClass(typeof(FamilySymbol))
                                      .OfCategory(BuiltInCategory.OST_Doors)
                                      .OfType<FamilySymbol>()
                                      .Where(x => x.Name.Equals("0915 x 2032mm"))
                                      .Where(x => x.FamilyName.Equals("M_Single-Flush"))
                                      .FirstOrDefault();
            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point0 = hostCurve.Curve.GetEndPoint(0);
            XYZ point1 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point0 + point1) / 2;

            if (!doorType.IsActive)
            {
                doorType.Activate();
            }

            doc.Create.NewFamilyInstance(point, doorType, wall, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
        }

        private void AddWindow(Document doc, Level level, Wall wall, double SillHeight)
        {
            FamilySymbol windowType = new FilteredElementCollector(doc)
                                      .OfClass(typeof(FamilySymbol))
                                      .OfCategory(BuiltInCategory.OST_Windows)
                                      .OfType<FamilySymbol>()
                                      .Where(x => x.Name.Equals("1050 x 1350mm"))
                                      .Where(x => x.FamilyName.Equals("M_Window-Casement-Double"))
                                      .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point0 = hostCurve.Curve.GetEndPoint(0);
            XYZ point1 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point0 + point1) / 2;
            point.Add(new XYZ(0, 0, SillHeight));

            if (!windowType.IsActive)
            {
                windowType.Activate();
            }

            FamilyInstance window = doc.Create.NewFamilyInstance(point, windowType, wall, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

            Parameter p_sillHeight = window.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM);
            p_sillHeight.Set(SillHeight);
        }

        private void AddRoof(Document doc, Level level, List<Wall> walls)
        {
            RoofType roofType = new FilteredElementCollector(doc)
                                    .OfClass(typeof(RoofType))
                                    .OfType<RoofType>()
                                    .Where(x => x.Name.Equals("Cold Roof - Concrete"))
                                    .Where(x => x.FamilyName.Equals("Basic Roof"))
                                    .FirstOrDefault();

            double wallWidth = walls[0].Width;
            double dt = wallWidth / 2;

            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dt, -dt, 0));
            points.Add(new XYZ(dt, -dt, 0));
            points.Add(new XYZ(dt, dt, 0));
            points.Add(new XYZ(-dt, dt, 0));
            points.Add(new XYZ(-dt, -dt, 0));


            Application application = doc.Application;

            CurveArray footprint = application.Create.NewCurveArray();
            for (int i = 0; i < walls.Count; i++)
            {
                LocationCurve curveLocation = walls[i].Location as LocationCurve;
                XYZ p0 = curveLocation.Curve.GetEndPoint(0);
                XYZ p1 = curveLocation.Curve.GetEndPoint(1);
                Line line = Line.CreateBound(p0 + points[i], p1 + points[i + 1]);

                footprint.Append(line);
            }

            ModelCurveArray footprintToModelCurveMapping = new ModelCurveArray();
            FootPrintRoof footprintRoof = doc.Create.NewFootPrintRoof(footprint, level, roofType, out footprintToModelCurveMapping);

            foreach (ModelCurve modelCurve in footprintToModelCurveMapping)
            {
                footprintRoof.set_DefinesSlope(modelCurve, true);
                footprintRoof.set_SlopeAngle(modelCurve, 0.5);
            }
        }

        private void AddExtrusionRoof(Document doc, Level level, List<Wall> walls)
        {
            RoofType roofType = new FilteredElementCollector(doc)
                                   .OfClass(typeof(RoofType))
                                   .OfType<RoofType>()
                                   .Where(x => x.Name.Equals("Cold Roof - Concrete"))
                                   .Where(x => x.FamilyName.Equals("Basic Roof"))
                                   .FirstOrDefault();

            double wallWidth = walls[0].Width;
            double dt = wallWidth / 2;

            double heightStart = level.get_Parameter(BuiltInParameter.LEVEL_ELEV).AsDouble() + 
                                 roofType.get_Parameter(BuiltInParameter.ROOF_ATTR_DEFAULT_THICKNESS_PARAM).AsDouble();
            LocationCurve wallCurve = walls[1].Location as LocationCurve;
            XYZ p0 = wallCurve.Curve.GetEndPoint(0);
            XYZ p1 = wallCurve.Curve.GetEndPoint(1);

            CurveArray curveArray = new CurveArray();
            curveArray.Append(Line.CreateBound(new XYZ(p0.X, p0.Y-dt, heightStart),
                                               new XYZ((p0.X + p1.X) / 2, (p0.Y + p1.Y) / 2, heightStart + UnitUtils.ConvertToInternalUnits(500, UnitTypeId.Millimeters))));
            curveArray.Append(Line.CreateBound(new XYZ((p0.X + p1.X) / 2, (p0.Y + p1.Y) / 2, heightStart + UnitUtils.ConvertToInternalUnits(500, UnitTypeId.Millimeters)),
                                               new XYZ(p1.X, p1.Y+dt, heightStart)));

            LocationCurve wallCurveForExtrusion = walls[0].Location as LocationCurve;
            XYZ start = wallCurveForExtrusion.Curve.GetEndPoint(0);
            XYZ end = wallCurveForExtrusion.Curve.GetEndPoint(1);

            ReferencePlane plane = doc.Create.NewReferencePlane(new XYZ(0, 0, 0), new XYZ(0, 0, 20), new XYZ(0, 20, 0), doc.ActiveView);
            doc.Create.NewExtrusionRoof(curveArray, plane, level, roofType, start.X-dt, end.X+dt);

        }

    }
}
