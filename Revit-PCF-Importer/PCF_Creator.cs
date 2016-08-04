﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.ApplicationServices;
using BuildingCoder;
using PCF_Functions;
using iv = PCF_Functions.InputVars;

namespace Revit_PCF_Importer
{
    public interface ICreateElements
    {
        Result SendElementsToCreation(ElementSymbol elementSymbol);
        }

    public interface IProcessElements
    {
        //Top level keywords
        Result ELEMENT_TYPE_NOT_IMPLEMENTED(ElementSymbol elementSymbol);
        Result PIPE(ElementSymbol elementSymbol);
        Result ELBOW(ElementSymbol elementSymbol);
        Result TEE(ElementSymbol elementSymbol);
}

    public class ProcessElements : IProcessElements
    {
        private Document doc = PCFImport.doc;
        public Result ELEMENT_TYPE_NOT_IMPLEMENTED(ElementSymbol elementSymbol)
        {
            return Result.Succeeded;
        }

        public Result PIPE(ElementSymbol elementSymbol)
        {
            try
            {
                //Choose pipe type. Hardcoded value until a configuring process is devised.
                ElementId pipeTypeId = new ElementId(3048519); //Hardcoded until configuring process is implemented

                //Collect levels and select one level
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                ElementClassFilter levelFilter = new ElementClassFilter(typeof(Level));
                ElementId levelId = collector.WherePasses(levelFilter).FirstElementId();

                //Create pipe
                Pipe pipe = Pipe.Create(doc, elementSymbol.PipingSystemType.Id, pipeTypeId, levelId,
                    elementSymbol.EndPoint1.Xyz, elementSymbol.EndPoint2.Xyz);
                //Set pipe diameter
                Parameter parameter = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);

                parameter.Set(elementSymbol.EndPoint1.Diameter);

                //Store the reference for the created element in the ElementSymbol
                elementSymbol.CreatedElement = (Element) pipe;
                return Result.Succeeded;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return Result.Failed;
            }
        }

        public Result ELBOW(ElementSymbol elementSymbol)
        {
            try
            {
                //Choose pipe type.Hardcoded value until a configuring process is devised.
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                Filter filter = new Filter("EN 10253-2 - Elbow: 3D", BuiltInParameter.SYMBOL_FAMILY_AND_TYPE_NAMES_PARAM); //Hardcoded until implements
                //Applying a fast filter first (OfCategory) to reduce load on slow filter (WherePasses).
                FamilySymbol elbowSymbol = collector.OfCategory(BuiltInCategory.OST_PipeFitting).WherePasses(filter.epf).Cast<FamilySymbol>().FirstOrDefault();
                if (elbowSymbol == null)
                {
                    Util.ErrorMsg("Family and Type for ELBOW at position " + elementSymbol.Position + " was not found.");
                    return Result.Failed;
                }

                #region Placing by face reference

                ////Build a direct shape with TessellatedShapeBuilder
                //List<XYZ> args = new List<XYZ>(3);

                //TessellatedShapeBuilder builder = new TessellatedShapeBuilder();
                ////http://thebuildingcoder.typepad.com/blog/2014/05/directshape-performance-and-minimum-size.html
                //builder.OpenConnectedFaceSet(false);
                //args.Add(elementSymbol.CentrePoint.Xyz);
                //args.Add(elementSymbol.EndPoint1.Xyz);
                //args.Add(elementSymbol.EndPoint2.Xyz);
                //builder.AddFace(new TessellatedFace(args,ElementId.InvalidElementId));
                //builder.CloseConnectedFaceSet();
                //builder.Build();
                //TessellatedShapeBuilderResult result = builder.GetBuildResult();

                //var resultList = result.GetGeometricalObjects();

                //var solidShape = resultList[0] as Solid;
                //Face face = solidShape.Faces.get_Item(0);

                //DirectShape ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                //ds.ApplicationId = "Application id";
                //ds.ApplicationDataId = "Geometry object id";
                //ds.Name = "Elbow " + elementSymbol.Position;
                //DirectShapeOptions dso = ds.GetOptions();
                //dso.ReferencingOption = DirectShapeReferencingOption.Referenceable;
                //ds.SetOptions(dso);
                //ds.SetShape(resultList);
                //Options options = new Options();
                //options.ComputeReferences = true;
                //doc.Regenerate();
                //FilteredElementCollector collectorDs = new FilteredElementCollector(doc);
                //collectorDs.OfClass(typeof (DirectShape));
                //var query = from Element e in collectorDs
                //    where string.Equals(e.Name, "Elbow " + elementSymbol.Position)
                //    select e;
                //Element elem = query.FirstOrDefault();
                //var geometryElement = elem.get_Geometry(options);
                //;
                //Face face = null;
                //foreach (GeometryObject geometry in geometryElement)
                //{
                //    Solid instance = geometry as Solid;
                //    if (null == instance || 0 == instance.Faces.Size || 0 == instance.Edges.Size) { continue; }
                //    // Get the faces
                //    foreach (Face f in instance.Faces)
                //    {
                //        face = f;
                //    }
                //}
                //;
                //if (face == null)
                //{
                //    Util.ErrorMsg("No valid face detected to place the fitting for element at position "+elementSymbol.Position);
                //    return Result.Failed;
                //}
                //var faceRef = face.Reference;
                //;
                ////Finally place the elbow
                ////Direction -- third parameter to the create method
                //XYZ vC = elementSymbol.EndPoint1.Xyz - elementSymbol.CentrePoint.Xyz;
                //Element element = doc.Create.NewFamilyInstance(faceRef, elementSymbol.CentrePoint.Xyz, vC,
                //    (FamilySymbol) elbowSymbol);

                #endregion

                #region Create by NewElbowFitting
                //Get a list of all pipes created in project
                //Consider adding a filter to only work on pipes in the same pipeline
                var query = (from ElementSymbol es in PCFImport.ExtractedElementCollection.Elements
                             where string.Equals("PIPE", es.ElementType)
                             select (MEPCurve)es.CreatedElement).ToList();
                //Remove all null items from list
                //query.RemoveAll(item => item == null);
                
                //Collect all pipe connectors present im project while filtering for end connector types
                IList<Connector> allConnectors = query
                    .Select(mepCurve => mepCurve.ConnectorManager.Connectors)
                    .SelectMany(conSet => (from Connector c in conSet
                                           where (int)c.ConnectorType == 1
                                           select c)).ToList();

                //Get the actual endpoints of the elbow
                XYZ p1 = elementSymbol.EndPoint1.Xyz; XYZ p2 = elementSymbol.EndPoint2.Xyz;
                //Determine the corresponding pipe connectors
                var c1 = (from Connector c in allConnectors where Util.IsEqual(p1, c.Origin) select c).FirstOrDefault();
                var c2 = (from Connector c in allConnectors where Util.IsEqual(p2, c.Origin) select c).FirstOrDefault();
                
                //Handle the missing connectors by creating dummy pipes
                //Choose pipe type. Hardcoded value until a configuring process is devised.
                ElementId pipeTypeId = new ElementId(3048519); //Hardcoded until configuring process is implemented

                //Collect levels and select one level
                FilteredElementCollector levelCollector = new FilteredElementCollector(doc);
                ElementClassFilter levelFilter = new ElementClassFilter(typeof(Level));
                ElementId levelId = levelCollector.WherePasses(levelFilter).FirstElementId();
                //Prepare placeholder pipes.
                Pipe pipe1 = null; Pipe pipe2 = null;

                if (c1 == null)
                {
                    //Create vector to define pipe
                    XYZ pipeDir = p1 - elementSymbol.CentrePoint.Xyz;
                    XYZ helperPoint1 = p1.Add(pipeDir.Multiply(2));
                    //Create pipe
                    pipe1 = Pipe.Create(doc, elementSymbol.PipingSystemType.Id, pipeTypeId, levelId,
                        p1, helperPoint1);
                    //Set pipe diameter
                    Parameter parameter = pipe1.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                    parameter.Set(elementSymbol.EndPoint1.Diameter);
                    c1 = (from Connector c in pipe1.ConnectorManager.Connectors
                        where Util.IsEqual(p1, c.Origin)
                          select c).FirstOrDefault();
                }

                if (c2 == null)
                {
                    //Create vector to define pipe
                    XYZ pipeDir = p2 - elementSymbol.CentrePoint.Xyz;
                    XYZ helperPoint2 = p2.Add(pipeDir.Multiply(2));
                    //Create pipe
                    pipe2 = Pipe.Create(doc, elementSymbol.PipingSystemType.Id, pipeTypeId, levelId,
                        p2, helperPoint2);
                    //Set pipe diameter
                    Parameter parameter = pipe2.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                    parameter.Set(elementSymbol.EndPoint2.Diameter);
                    c2 = (from Connector c in pipe2.ConnectorManager.Connectors
                            where Util.IsEqual(p2, c.Origin)
                          select c).FirstOrDefault();
                }

                if (c1 != null && c2 != null)
                {
                    Element element = doc.Create.NewElbowFitting(c1, c2);
                    if (pipe1 != null) doc.Delete(pipe1.Id);
                    if (pipe2 != null) doc.Delete(pipe2.Id);
                    elementSymbol.CreatedElement = element;
                    doc.Regenerate();
                    return Result.Succeeded;
                }
                //If this point is reached, something has failed
                return Result.Failed;
                #endregion

                #region Create by Directly Placing families

                //Element element = doc.Create.NewFamilyInstance(elementSymbol.CentrePoint.Xyz, (FamilySymbol)elbowSymbol, StructuralType.NonStructural);

                //FamilyInstance elbow = (FamilyInstance)element;

                //double diameter = elementSymbol.EndPoint1.Diameter;

                //elbow.LookupParameter("Nominal Diameter").Set(diameter); //Implement a procedure to select the parameter by name supplied by user

                ////Begin geometric analysis to rotate the endpoints to actual locations
                ////Get connectors from the placed family
                //ConnectorSet cs = ((FamilyInstance)element).MEPModel.ConnectorManager.Connectors;

                //Connector familyConnector1 = (from Connector c in cs where true select c).First();
                //Connector familyConnector2 = (from Connector c in cs where true select c).Last();

                //XYZ vA = familyConnector1.Origin - elementSymbol.CentrePoint.Xyz; //To define a vector: v = p2 - p1
                //XYZ vC = elementSymbol.EndPoint1.Xyz - elementSymbol.CentrePoint.Xyz;

                //XYZ vB = familyConnector2.Origin - elementSymbol.CentrePoint.Xyz; //To define a vector: v = p2 - p1
                //XYZ vD = elementSymbol.EndPoint2.Xyz - elementSymbol.CentrePoint.Xyz;

                //XYZ normRotAxis = vC.CrossProduct(vA).Normalize();

                //#region Fun with model lines

                //Filter filtermarker = new Filter("Marker: Marker", BuiltInParameter.SYMBOL_FAMILY_AND_TYPE_NAMES_PARAM); //Hardcoded until implements
                //FamilySymbol markerSymbol = new FilteredElementCollector(doc).WherePasses(filtermarker.epf).Cast<FamilySymbol>().FirstOrDefault();

                //XYZ A = vC.CrossProduct(vA);
                //XYZ B = vD.CrossProduct(vB);

                //Element marker = doc.Create.NewFamilyInstance(elementSymbol.CentrePoint.Xyz.Add(A), markerSymbol, StructuralType.NonStructural);
                ////Helper.PlaceAdaptiveMarkerLine("Red", elementSymbol.CentrePoint.Xyz, elementSymbol.CentrePoint.Xyz.Add(A));
                ////Helper.PlaceAdaptiveMarkerLine("Green", elementSymbol.CentrePoint.Xyz, elementSymbol.CentrePoint.Xyz.Add(B));


                //#endregion
                //Line rotLine = Line.CreateBound(elementSymbol.CentrePoint.Xyz, elementSymbol.EndPoint1.Xyz);
                //double rotAngle = Math.PI;

                ////double dotProduct = vC.DotProduct(vA);
                ////double rotAngle = System.Math.Acos(dotProduct);
                ////var rotLine = Line.CreateUnbound(elementSymbol.CentrePoint.Xyz, normRotAxis);

                //////Test rotation
                ////Transform trf = Transform.CreateRotationAtPoint(normRotAxis, rotAngle, elementSymbol.CentrePoint.Xyz);
                ////XYZ testRotation = trf.OfVector(vA).Normalize();

                ////if ((vC.DotProduct(testRotation) > 0.00001) == false) rotAngle = -rotAngle;


                //elbow.Location.Rotate(rotLine, rotAngle);

                ////Store the reference of the created element in the symbol object.
                //elementSymbol.CreatedElement = element;
                //;

                #endregion
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return Result.Succeeded;
        }

        public Result TEE(ElementSymbol elementSymbol)
        {
            try
            {
                //Choose pipe type.Hardcoded value until a configuring process is devised.
                
                FilteredElementCollector collector = new FilteredElementCollector(doc);

                //Determine if the tee is reducing
                if (!elementSymbol.Branch1Point.Diameter.Equals(elementSymbol.EndPoint1.Diameter))
                    elementSymbol.IsReducing = true;
                Filter filter;
                if (!elementSymbol.IsReducing) filter = new Filter("EN 10253-2 - Tee: Tee Type B", BuiltInParameter.SYMBOL_FAMILY_AND_TYPE_NAMES_PARAM);
                else filter = new Filter("EN 10253-2 - Reducing Tee: Red Tee Type B", BuiltInParameter.SYMBOL_FAMILY_AND_TYPE_NAMES_PARAM);
                FamilySymbol teeSymbol = collector.WherePasses(filter.epf).Cast<FamilySymbol>().FirstOrDefault();

                if (teeSymbol == null)
                {
                    Util.ErrorMsg("Family and Type for TEE at position " + elementSymbol.Position + " was not found.");
                    return Result.Failed;
                }

                Element element = doc.Create.NewFamilyInstance(elementSymbol.CentrePoint.Xyz, teeSymbol, StructuralType.NonStructural);

                FamilyInstance tee = (FamilyInstance)element;

                double mainDiameter = elementSymbol.EndPoint1.Diameter;
                double branchDiameter = elementSymbol.Branch1Point.Diameter;

                tee.LookupParameter("Nominal Diameter 1").Set(mainDiameter); //Implement a procedure to select the parameter by name supplied by user
                if (elementSymbol.IsReducing) tee.LookupParameter("Nominal Diameter 3").Set(branchDiameter);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }


            return Result.Succeeded;
        }
    }
}
