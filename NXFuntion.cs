using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Windows.Forms;
using NXOpen;
using NXOpenUI;
using NXOpen.UF;
using NXOpen.Features;
using NXOpen.GeometricUtilities;
using NXOpen.Annotations;
using NXOpen.Assemblies;
using System.Xml;

namespace NXFunctions
{
    class NxFuntion
    {
        private static Session theSession = Session.GetSession();
        private static UFSession theUFSession = UFSession.GetUFSession();
        private static Part displayPart = theSession.Parts.Display;
        private static UI theUI = UI.GetUI();

        public void Paralleldimension(Part workPart)
        {
            Body body = workPart.Bodies.ToArray()[0];
            Face[] all_face = body.GetFaces();
            List<double> points = new List<double>();
            List<Face> faces = new List<Face>();
            foreach (Face check_face in all_face)
            {
                if (check_face.SolidFaceType.ToString() == "Cylindrical")
                {
                    int type;
                    double[] point = new double[6];
                    double[] dir = new double[5];
                    double[] box = new double[6];
                    double radius;
                    double rad_data;
                    int norm_dir;
                    theUFSession.Modl.AskFaceData(check_face.Tag, out type, point, dir, box, out radius, out rad_data, out norm_dir);
                    points.Add(point[0]);
                    faces.Add(check_face);
                }
            }
            Dimension[] all_dimension = workPart.Dimensions.ToArray();
            double z = 10;
            Dimension check = null;
            //按大小排序
            for (int i = 0; i < all_dimension.Length; i++)
            {
                check = all_dimension[i];
                for (int j = i + 1; j < all_dimension.Length; j++)
                {
                    if (check.ComputedSize >= all_dimension[j].ComputedSize)
                    {
                        check = all_dimension[j];
                        all_dimension[j] = all_dimension[i];//排序需要将大的往后放
                        all_dimension[i] = check;
                    }
                }
            }
            foreach (Dimension pmidimension in all_dimension)
            {
                if (pmidimension.GetType().ToString() == "NXOpen.Annotations.PmiParallelDimension")
                {
                    Associativity ass1 = pmidimension.GetAssociativity(1);
                    Associativity ass2 = pmidimension.GetAssociativity(2);
                    Edge edge1 = (Edge)ass1.FirstObject;
                    Edge edge2 = (Edge)ass2.FirstObject;
                    Face[] face1 = edge1.GetFaces();
                    Face[] face2 = edge2.GetFaces();
                    double loc1 = 0;
                    double loc2 = 0;
                    //找到当前尺寸的两个平面的端面位置
                    foreach (Face check_face1 in face1)
                    {
                        if (check_face1.SolidFaceType.ToString() == "Planar")
                        {
                            int type;
                            double[] point = new double[6];
                            double[] dir = new double[5];
                            double[] box = new double[6];
                            double radius;
                            double rad_data;
                            int norm_dir;
                            theUFSession.Modl.AskFaceData(check_face1.Tag, out type, point, dir, box, out radius, out rad_data, out norm_dir);
                            loc1 = point[0];
                            break;
                        }
                    }
                    foreach (Face check_face2 in face2)
                    {
                        if (check_face2.SolidFaceType.ToString() == "Planar")
                        {
                            int type;
                            double[] point = new double[6];
                            double[] dir = new double[5];
                            double[] box = new double[6];
                            double radius;
                            double rad_data;
                            int norm_dir;
                            theUFSession.Modl.AskFaceData(check_face2.Tag, out type, point, dir, box, out radius, out rad_data, out norm_dir);
                            loc2 = point[0];
                            break;
                        }
                    }
                    double temp = 0;
                    List<NXObject> delete = new List<NXObject>();
                    foreach (double point in points)
                    {
                        if (((point < loc1) & (point > loc2)) || ((point > loc1) & (point < loc2)))
                        {
                            Face face = faces[points.IndexOf(point)];
                            Dimension check_points = null;
                            Point3d helppoint = new Point3d(0, 0, 0);
                            Cylindricaldimension(face, helppoint, out check_points);
                            if (temp < check_points.ComputedSize)
                            {
                                temp = check_points.ComputedSize;
                            }
                            delete.Add(check_points);
                        }
                    }
                    NXObject[] result = delete.ToArray();
                    DeleteObject(result);
                    Point3d result_point = pmidimension.AnnotationOrigin;
                    result_point.Y = temp + z;//可能要与具体轴的方位有关
                    pmidimension.AnnotationOrigin = result_point;
                    z = z + 10;
                }
            }
        }

        #region 作布尔差得到去除的制造体,输出一个特征
        public void createCollector(Body targetbody, Body toolbody, out Feature succuessfeature)
        {
            Part workPart = theSession.Parts.Work;//获得工作部件
            BooleanFeature nullFeatures_BooleanFeature = null;
            BooleanBuilder booleanBuilder1;
            booleanBuilder1 = workPart.Features.CreateBooleanBuilderUsingCollector(nullFeatures_BooleanFeature);
            //添加目标实体
            ScCollector scCollector1;
            scCollector1 = booleanBuilder1.ToolBodyCollector;
            booleanBuilder1.Tolerance = 0.001;
            booleanBuilder1.Operation = NXOpen.Features.Feature.BooleanType.Subtract;
            bool added1;
            added1 = booleanBuilder1.Targets.Add(targetbody);
            //添加刀具实体
            ScCollector scCollector2;
            scCollector2 = workPart.ScCollectors.CreateCollector();
            Body[] bodies1 = new Body[1];
            Body body2 = toolbody;
            bodies1[0] = body2;
            BodyDumbRule bodyDumbRule1;
            bodyDumbRule1 = workPart.ScRuleFactory.CreateRuleBodyDumb(bodies1);
            SelectionIntentRule[] rules1 = new SelectionIntentRule[1];
            rules1[0] = bodyDumbRule1;
            scCollector2.ReplaceRules(rules1, false);
            booleanBuilder1.ToolBodyCollector = scCollector2;
            booleanBuilder1.CopyTools = true;//让当前工序模型仍然存在
            NXObject nXObject1;
            nXObject1 = booleanBuilder1.Commit();
            succuessfeature = booleanBuilder1.CommitFeature();
            booleanBuilder1.Destroy();
        }
        #endregion

        #region Wave链接实体
        //整个wave操作相当于：设置目标部件为工作部件→建立链接→将要插入的部件设置为工作部件→提取它→重新将目标部件设置为工作部件→提交操作
        public void createwaves(Part oriPart, Part link_part)
        {
            try
            {
                Part workPart = theSession.Parts.Work;
                #region 对链接进行各项操作设定
                Feature nullFeatures_Feature = null;
                WaveLinkBuilder waveLinkBuilder1;
                waveLinkBuilder1 = workPart.BaseFeatures.CreateWaveLinkBuilder(nullFeatures_Feature);
                ExtractFaceBuilder extractFaceBuilder1;
                extractFaceBuilder1 = waveLinkBuilder1.ExtractFaceBuilder;

                extractFaceBuilder1.FaceOption = ExtractFaceBuilder.FaceOptionType.FaceChain;
                waveLinkBuilder1.Type = WaveLinkBuilder.Types.BodyLink;
                extractFaceBuilder1.FaceOption = ExtractFaceBuilder.FaceOptionType.FaceChain;
                extractFaceBuilder1.AngleTolerance = 45.0;
                extractFaceBuilder1.ParentPart = ExtractFaceBuilder.ParentPartType.OtherPart;
                extractFaceBuilder1.Associative = true;
                extractFaceBuilder1.FixAtCurrentTimestamp = false;
                extractFaceBuilder1.HideOriginal = false;
                extractFaceBuilder1.InheritDisplayProperties = false;
                #endregion
                SelectObjectList selectObjectList1;
                selectObjectList1 = extractFaceBuilder1.BodyToExtract;
                //将链接部件设置为工作部件
                theUFSession.Assem.SetWorkPart(link_part.Tag);
                Body[] addBodies = link_part.Bodies.ToArray();
                //添加实体
                Component component1 = (Component)displayPart.ComponentAssembly.RootComponent.FindObject("COMPONENT " + link_part.JournalIdentifier + " 1");
                //得到部件里的所有的，link_part就是得到part的名字，我们就可以通用操作了
                Body body1 = (Body)component1.FindObject("PROTO#.Bodies|" + addBodies[0].JournalIdentifier);//只加一个体，所以不能加多个体，可以试着用循环
                bool added1;
                added1 = selectObjectList1.Add(body1);
                //将初始部件设置为工作部件
                theUFSession.Assem.SetWorkPart(oriPart.Tag);
                #region 相当于提交操作，生成文件
                NXObject nXObject1;
                nXObject1 = waveLinkBuilder1.Commit();
                waveLinkBuilder1.Destroy();
                #endregion
            }
            catch (Exception ex)
            {
                theUI.NXMessageBox.Show("提示", NXMessageBox.DialogType.Error, ex.ToString());
            }
        }
        #endregion

        #region 获取当前工制造特征体的接触面
        public void GetInterFace(Body manubody, Body toolbody, out List<Face> InterFace)
        {
            InterFace = new List<Face>();
            //不能写成List<Face>InterFace=new List<Face>(),这样会不断要求赋值
            //更不能写成InterFace，那样的话就会一直是空的
            Face[] stepMarkFaces = toolbody.GetFaces();//当前工序,之前作为tool存在
            Tag[] stepMarkFaceTags = new Tag[stepMarkFaces.Length];
            for (int i = 0; i < stepMarkFaces.Length; i++)
            {
                stepMarkFaceTags[i] = stepMarkFaces[i].Tag;
            }
            Face[] stepManuFaces = manubody.GetFaces();//制造特征体
            for (int i = 0; i < stepManuFaces.Length; i++)
            {
                int[] interResults = new int[stepMarkFaces.Length];
                theUFSession.Modl.CheckInterference(stepManuFaces[i].Tag, stepMarkFaces.Length, stepMarkFaceTags, interResults);
                for (int j = 0; j < interResults.Length; j++)
                {
                    if (interResults[j] == 1)
                    {
                        InterFace.Add(stepMarkFaces[j]);
                        break;
                    }
                }
            }
        }
        #endregion

        #region 删除对象（可用于删除制造特征体和尺寸）
        public void DeleteObject(NXObject[] obj)
        {
            try
            {
                bool notifyOnDelete1;
                notifyOnDelete1 = theSession.Preferences.Modeling.NotifyOnDelete;
                theSession.UpdateManager.ClearErrorList();
                NXOpen.Session.UndoMarkId markId1;
                markId1 = theSession.SetUndoMark(Session.MarkVisibility.Visible, "Delete");
                int nErrs1;
                nErrs1 = theSession.UpdateManager.AddToDeleteList(obj);
                bool notifyOnDelete2;
                notifyOnDelete2 = theSession.Preferences.Modeling.NotifyOnDelete;
                int nErrs2;
                nErrs2 = theSession.UpdateManager.DoUpdate(markId1);
            }
            catch (Exception ex)
            {
                UI.GetUI().NXMessageBox.Show("提示信息", NXMessageBox.DialogType.Information, ex.ToString());
            }
        }
        #endregion

        #region WAVE→链接→布尔差→得到接触面→标注特殊模型→获取需要根据继承来标注的轴向端面
        public void GetMarkFace(Part start_part, Part link_part,out List<Face>Mark_Face)//模块化，这个模块的功能是能够让两个模型之间进行WAVE链接比较
        {
            #region 进行布尔差
            //进行链接，link_part放入start_part中
            theUFSession.Assem.SetWorkPart(start_part.Tag);//必须把目标实体设置成工作部件
            createwaves(start_part, link_part);
            //进行布尔差，此时目标实体已经是工作部件了
            Part workPart = theSession.Parts.Work;
            Body[] allBody = workPart.Bodies.ToArray();
            Body toolbody = allBody[0];//当前工序模型
            Body targetbody = allBody[1];//这便是链接进去的实体
            Feature feature;
            createCollector(targetbody, toolbody, out feature);
            BodyFeature subBodyFeature = (BodyFeature)feature;
            Body manuBody = subBodyFeature.GetBodies()[0];//得到制造特征体
            #endregion

            #region 获得所有要标注的面
            List<Body> deleteBody = new List<Body>();
            List<Face> InterMarkFace = new List<Face>();
            GetInterFace(manuBody, toolbody, out InterMarkFace);
            Face[] MarkFace = InterMarkFace.ToArray();//得到所有标注所需的面
            #endregion

            #region 得到轴向标注的面和径向标注的面
            List<Face> axis_dimension_face = new List<Face>();
            List<Edge> axis_dimension_edge = new List<Edge>();
            List<Face> arc_dimension_face = new List<Face>();
            List<Face> dia_dimension_face = new List<Face>();
            foreach (Face check_face in MarkFace)
            {
                int type;
                double[] point = new double[6];
                double[] dir = new double[5];
                double[] box = new double[6];
                double radius;
                double rad_data;
                int norm_dir;
                theUFSession.Modl.AskFaceData(check_face.Tag, out type, point, dir, box, out radius, out rad_data, out norm_dir);
                //得到平面的类型
                if (type == 22)//端面
                {
                    axis_dimension_face.Add(check_face);
                }
                else if (type == 16)//圆柱面
                {
                    dia_dimension_face.Add(check_face);
                }
                else if (type == 18)
                {
                    arc_dimension_face.Add(check_face);
                }
            }
            Mark_Face = axis_dimension_face;//把我们要用通过继承来标注的面输出出去
            #endregion

            PartLoadStatus partLoadStatus1;
            theSession.Parts.SetDisplay(workPart, true, true, out partLoadStatus1);
            Layout layout1 = (Layout)workPart.Layouts.FindObject("L1");
            ModelingView modelview = workPart.ModelingViews.WorkView;
            string strModelView = modelview.Name;
            string viewName = "RIGHT";
            if (strModelView != viewName)
            {
                ModelingView modelingView1 = (ModelingView)workPart.ModelingViews.FindObject(viewName);
                layout1.ReplaceView(workPart.ModelingViews.WorkView, modelingView1, true);
            }

            #region 在这里就可以对径向进行标注,但发现因为在检查退刀槽时也进行了对比，导致径向尺寸会标注两次
            double y = 10;
            double temp = 0;

            foreach (Face mark_dia_face in dia_dimension_face)
            {
                temp = temp + 1;
                Point3d helppoint = new Point3d(0,0,0);
                Dimension dim = null;
                Cylindricaldimension(mark_dia_face, helppoint, out dim);
                Point3d check_point1;
                check_point1 = dim.AnnotationOrigin;
                check_point1.Z = 0.0;
                check_point1.Y = dim.ComputedSize + y;
                dim.AnnotationOrigin = check_point1;
                dim.IsOriginCentered = true;
                y = y + 10;
            }
            #endregion
            
            #region 对圆弧面进行标注
            foreach (Face mark_arc_face in arc_dimension_face)
            {
                //引用之前的方法
                //之前已经把工作部件设置为当前工序模型了，所以不用再次设置
                DatumPlane mark_plane;
                IntersectionCurve mark_curve;
                createdatadum(mark_arc_face, out mark_plane);
                createlinkcurve(mark_plane,mark_arc_face,out mark_curve);
                createArcDimension(mark_curve);
            }
            #endregion 
            
            #region 删除加进来的制造体
            deleteBody.Add(manuBody);
            NXObject[] deleteObject = deleteBody.ToArray();
            DeleteObject(deleteObject);
            #endregion

            //完成后，对视图的标注改成后视图，为轴向标注做准备
            viewName = "BACK";
            if (strModelView != viewName)
            {
                ModelingView modelingView1 = (ModelingView)workPart.ModelingViews.FindObject(viewName);
                layout1.ReplaceView(workPart.ModelingViews.WorkView, modelingView1, true);
            }
        }
        #endregion 

        #region 输入待检测的平面，将其按照X轴正方向从大到小输出,用于粗加工，内含对退刀槽的检测
        public void GetFaceWithPoint_Rough(Face[] checkface,out List<Face> CompareFace, out List<double>ComparePoint)
        {
            List<Face> CompareFace1 = new List<Face>();
            List<double> ComparePoint1=new List<double>();
            double temp = 0;//用来处理第一次
            foreach (Face check_face1 in checkface)
            {
                double[] point_num_get = ComparePoint1.ToArray();
                int num = point_num_get.Length;//得到点的个数，从而应对如果现在判断的点是最小的
                int type;
                double[] point = new double[6];
                double[] dir = new double[5];
                double[] box = new double[6];
                double radius;
                double rad_data;
                int norm_dir;
                theUFSession.Modl.AskFaceData(check_face1.Tag, out type, point, dir, box, out radius, out rad_data, out norm_dir);
                if (type == 22)
                {
                    if (temp == 0)//最开始
                    {
                        CompareFace1.Add(check_face1);
                        temp = temp + 1;
                        ComparePoint1.Add(point[0]);
                    }
                    else
                    {//对面在轴上的位置进行判断，按照从大到小的顺序,利用点的集合
                        int index = 0;
                        foreach (double check_point in ComparePoint1)
                        {
                            #region 尝试对退刀槽进行检查，如果失败，想要回滚，直接删掉if部分，然后把else去掉就好
                            //这里要加入退刀槽的检测，到时用一个else把下头的代码包括进去
                            double check_num=0;
                            check_num = Math.Abs(check_point - point[0]);
                            if (check_num <= 4)//检测为疑似退刀槽对象
                            {
                                int choose;
                                CheckRectorFace(CompareFace1[index], check_face1, out choose);//前者为当前对应的点，因为check_point的位置通过index与面绑定的
                                if (choose == 2)
                                {//列表里已经有的面就是我们要保留的面，当前面就不加进去了，映射不包括它
                                    break;
                                }
                                if (choose == 1)
                                {//把里头的那个给替换了
                                    Face removeFace = CompareFace1[index];
                                    ComparePoint1.Insert(index, point[0]);
                                    ComparePoint1.Remove(check_point);
                                    CompareFace1.Insert(index, check_face1);
                                    CompareFace1.Remove(removeFace);
                                    break;
                                }
                                //如果不中断的话，表明是误伤，继续执行之前的操作就好
                            }
                            else
                            {
                                if (check_point < point[0])
                                {
                                    ComparePoint1.Insert(index, point[0]);
                                    CompareFace1.Insert(index, check_face1);
                                    break;
                                }
                                index = index + 1;
                                if (index == num)
                                {
                                    ComparePoint1.Add(point[0]);
                                    CompareFace1.Add(check_face1);
                                    break;
                                }
                            }
                            #endregion 
                        }
                    }
                }
            }
            CompareFace = CompareFace1;
            ComparePoint = ComparePoint1;
        }
        #endregion
        
        #region 输入待检测的平面，将其按照X轴正方向从大到小输出,用于精加工对退刀槽并不检测
        public void GetFaceWithPoint_Finish(Face[] checkface, out List<Face> CompareFace, out List<double> ComparePoint)
                {
                    List<Face> CompareFace1 = new List<Face>();
                    List<double> ComparePoint1 = new List<double>();
                    double temp = 0;//用来处理第一次
                    foreach (Face check_face1 in checkface)
                    {
                        double[] point_num_get = ComparePoint1.ToArray();
                        int num = point_num_get.Length;//得到点的个数，从而应对如果现在判断的点是最小的
                        int type;
                        double[] point = new double[6];
                        double[] dir = new double[5];
                        double[] box = new double[6];
                        double radius;
                        double rad_data;
                        int norm_dir;
                        theUFSession.Modl.AskFaceData(check_face1.Tag, out type, point, dir, box, out radius, out rad_data, out norm_dir);
                        if (type == 22)
                        {
                            if (temp == 0)//最开始
                            {
                                CompareFace1.Add(check_face1);
                                temp = temp + 1;
                                ComparePoint1.Add(point[0]);
                            }
                            else
                            {//对面在轴上的位置进行判断，按照从大到小的顺序,利用点的集合
                                int index = 0;
                                foreach (double check_point in ComparePoint1)
                                {
                                        if (check_point < point[0])
                                        {
                                            ComparePoint1.Insert(index, point[0]);
                                            CompareFace1.Insert(index, check_face1);
                                            break;
                                        }
                                        index = index + 1;
                                        if (index == num)
                                        {
                                            ComparePoint1.Add(point[0]);
                                            CompareFace1.Add(check_face1);
                                            break;
                                        }
                                }
                            }
                        }
                    }
                    CompareFace = CompareFace1;
                    ComparePoint = ComparePoint1;
                }
        #endregion

        #region 检测退刀槽端面
        public void CheckRectorFace(Face formFace, Face compareFace,out int choose)
                {
                    choose = 0;
                    Edge[] edge1 = formFace.GetEdges();
                    Edge[] edge2 = compareFace.GetEdges();
                    List<Face> Compare1 = new List<Face>();//已经在列表里的
                    List<Face> Compare2 = new List<Face>();
                    #region 得到两个端面的圆柱面集合
                    foreach (Edge check_edge1 in edge1)
                    {//对每个边拥有的面进行检查
                        foreach (Face check_face1 in check_edge1.GetFaces())
                        {
                            if (check_face1.SolidFaceType.ToString() == "Cylindrical")
                            {//为圆柱面（我们要用来检测
                                Compare1.Add(check_face1);
                            }
                        }
                    }
                    foreach (Edge check_edge2 in edge2)
                    {
                        foreach (Face check_face2 in check_edge2.GetFaces())
                        {
                            if (check_face2.SolidFaceType.ToString() == "Cylindrical")
                            {
                                Compare2.Add(check_face2);
                            }
                        }
                    }
                    #endregion
                    #region 得到三个比较的圆柱面
                    Face CenterFace=null;
                    int temp = 0;
                    //根据实验，必须要加一个跳转指示
                    foreach (Face check_face1 in Compare1)
                    {
                        foreach(Face check_face2 in Compare2)
                        {
                            if (check_face1 == check_face2)//找到了那个公共面
                            {
                                CenterFace = check_face1;
                                Compare1.Remove(check_face1);
                                Compare2.Remove(check_face2);
                                temp = 1;
                                break;
                            }
                        }
                        if (temp == 1)
                        {
                            break;
                        }
                    }
                    //此时Compare1,Compare2,CenterFace分别对应三个圆柱面，进行检查
                    #endregion 
                    #region 利用圆柱面的尺寸进行比较
                    Face Compareface1 = Compare1[0];
                    Face Compareface2 = Compare2[0];
                    Dimension dim1;
                    Dimension dim2;
                    Dimension centerdim;
                    Point3d helppoint = new Point3d(0, 0, 0);
                    Cylindricaldimension(Compareface1,helppoint,out dim1);
                    Cylindricaldimension(Compareface2,helppoint, out dim2);
                    Cylindricaldimension(CenterFace, helppoint, out centerdim);
                    NXObject[] deletedimension = new NXObject[3];
                    deletedimension[0] = dim1;
                    deletedimension[1] = dim2;
                    deletedimension[2] = centerdim;
                    if((centerdim.ComputedSize<dim1.ComputedSize)&(centerdim.ComputedSize<dim2.ComputedSize))
                    {
                        if (dim1.ComputedSize > dim2.ComputedSize)
                        {//保留原来就存在里头的
                            choose = 2;
                            DeleteObject(deletedimension);
                            return;
                        }
                        if (dim1.ComputedSize < dim2.ComputedSize)
                        {//需要替换
                            choose = 1;
                            DeleteObject(deletedimension);
                            return;
                        }
                    }
                    #endregion
                }
        #endregion 
        /*对当前两个平面进行检测（未试验，准备用于退刀槽
        formFace为listpoint[index]处的面，而compareFaces是当前要检测的面
        /*formface对应的是CompareFace(index),compareFace对应的是check_face
        */

        //标注方法

        #region 构建用于存放尺寸的基准平面
        /// <summary>
        /// 
        /// </summary>
        /// <param name="position">放置位置</param>
        /// <param name="mark_datumplane">基准平面</param>
        public void createDatumPlane(Point3d position,out DatumPlane mark_datumplane)
        {
            Session theSession = Session.GetSession();
            Part workPart = theSession.Parts.Work;
            Part displayPart = theSession.Parts.Display;
            NXOpen.Features.Feature nullFeatures_Feature = null;
            NXOpen.Features.DatumPlaneBuilder datumPlaneBuilder1;
            datumPlaneBuilder1 = workPart.Features.CreateDatumPlaneBuilder(nullFeatures_Feature);
            Plane plane1;
            plane1 = datumPlaneBuilder1.GetPlane();
            Point3d coordinates1 = new Point3d(0.0, 0.0, 0.0);//规定原点
            Point point1;//标定相对位置，如距离原点50mm
            point1 = workPart.Points.CreatePoint(coordinates1);
            plane1.SetMethod(NXOpen.PlaneTypes.MethodType.FixedY);//设置一个平面类型，使平面垂直于Y轴
            NXObject[] geom1 = new NXObject[0];
            plane1.SetGeometry(geom1);
            Point3d origin1 = position;
            plane1.Origin = origin1;
            Matrix3x3 matrix1;
            matrix1.Xx = 0.0;
            matrix1.Xy = 0.0;
            matrix1.Xz = 1.0;
            matrix1.Yx = 1.0;
            matrix1.Yy = 0.0;
            matrix1.Yz = 0.0;
            matrix1.Zx = 0.0;
            matrix1.Zy = 1.0;
            matrix1.Zz = 0.0;
            plane1.Matrix = matrix1;
            //这一部分的作用是使平面翻转，如果删掉就变成了XY平面
            bool flip1;
            flip1 = plane1.Flip;
            NXOpen.Features.Feature feature1;
            feature1 = datumPlaneBuilder1.CommitFeature();
            NXOpen.Features.DatumPlaneFeature datumPlaneFeature1 = (NXOpen.Features.DatumPlaneFeature)feature1;
            DatumPlane datumPlane1;
            datumPlane1 = datumPlaneFeature1.DatumPlane;
            datumPlane1.SetReverseSection(true);//作用是反向，但没有用，可能是哪里出问题了
            mark_datumplane = datumPlane1;
            datumPlaneBuilder1.Destroy();
        }
        #endregion 

        #region 轴向尺寸标注
        public void createaxisdimension(Edge helpedge1, Edge helpedge2, out Dimension markdim)
        {
            markdim = null;
            Part workPart = theSession.Parts.Work;
            Part displayPart = theSession.Parts.Display;
            NXOpen.Annotations.DimensionData dimensionData1;
            dimensionData1 = workPart.Annotations.NewDimensionData();
            NXOpen.Annotations.Associativity associativity1;
            associativity1 = workPart.Annotations.NewAssociativity();
            Edge edge1 = helpedge1;
            associativity1.FirstObject = edge1;
            NXObject nullNXObject = null;
            associativity1.SecondObject = nullNXObject;
            associativity1.ObjectView = null;
            associativity1.PointOption = NXOpen.Annotations.AssociativityPointOption.ArcCenter;
            associativity1.LineOption = NXOpen.Annotations.AssociativityLineOption.None;
            Point3d firstDefinitionPoint1 = new Point3d(0.0, 0.0, 0.0);
            associativity1.FirstDefinitionPoint = firstDefinitionPoint1;
            Point3d secondDefinitionPoint1 = new Point3d(0.0, 0.0, 0.0);
            associativity1.SecondDefinitionPoint = secondDefinitionPoint1;
            associativity1.Angle = 0.0;
            Point3d pickPoint1 = new Point3d(0,0,0);
            associativity1.PickPoint = pickPoint1;
            NXOpen.Annotations.Associativity[] associativity2 = new NXOpen.Annotations.Associativity[1];
            associativity2[0] = associativity1;
            dimensionData1.SetAssociativity(1, associativity2);
            associativity1.Dispose();
            NXOpen.Annotations.Associativity associativity3;
            associativity3 = workPart.Annotations.NewAssociativity();
            Edge edge2 = helpedge2;
            associativity3.FirstObject = edge2;
            associativity3.SecondObject = nullNXObject;
            associativity3.ObjectView = null;//必须改成null
            associativity3.PointOption = NXOpen.Annotations.AssociativityPointOption.ArcCenter;
            associativity3.LineOption = NXOpen.Annotations.AssociativityLineOption.None;
            Point3d firstDefinitionPoint2 = new Point3d(0.0, 0.0, 0.0);
            associativity3.FirstDefinitionPoint = firstDefinitionPoint2;
            Point3d secondDefinitionPoint2 = new Point3d(0.0, 0.0, 0.0);
            associativity3.SecondDefinitionPoint = secondDefinitionPoint2;
            associativity3.Angle = 0.0;
            Point3d pickPoint2 = new Point3d(0,0,0);
            associativity3.PickPoint = pickPoint2;
            NXOpen.Annotations.Associativity[] associativity4 = new NXOpen.Annotations.Associativity[1];
            associativity4[0] = associativity3;
            dimensionData1.SetAssociativity(2, associativity4);
            associativity3.Dispose();
            NXOpen.Annotations.DimensionPreferences dimensionPreferences1;
            dimensionPreferences1 = workPart.Annotations.Preferences.GetDimensionPreferences();
            NXOpen.Annotations.OrdinateDimensionPreferences ordinateDimensionPreferences1;
            ordinateDimensionPreferences1 = dimensionPreferences1.GetOrdinateDimensionPreferences();
            ordinateDimensionPreferences1.Dispose();
            NXOpen.Annotations.ChamferDimensionPreferences chamferDimensionPreferences1;
            chamferDimensionPreferences1 = dimensionPreferences1.GetChamferDimensionPreferences();
            chamferDimensionPreferences1.Dispose();
            NXOpen.Annotations.NarrowDimensionPreferences narrowDimensionPreferences1;
            narrowDimensionPreferences1 = dimensionPreferences1.GetNarrowDimensionPreferences();
            narrowDimensionPreferences1.DimensionDisplayOption = NXOpen.Annotations.NarrowDisplayOption.None;
            dimensionPreferences1.SetNarrowDimensionPreferences(narrowDimensionPreferences1);
            narrowDimensionPreferences1.Dispose();
            NXOpen.Annotations.UnitsFormatPreferences unitsFormatPreferences1;
            unitsFormatPreferences1 = dimensionPreferences1.GetUnitsFormatPreferences();
            unitsFormatPreferences1.Dispose();
            NXOpen.Annotations.DiameterRadiusPreferences diameterRadiusPreferences1;
            diameterRadiusPreferences1 = dimensionPreferences1.GetDiameterRadiusPreferences();
            diameterRadiusPreferences1.Dispose();
            dimensionData1.SetDimensionPreferences(dimensionPreferences1);
            dimensionPreferences1.Dispose();
            NXOpen.Annotations.LineAndArrowPreferences lineAndArrowPreferences1;
            lineAndArrowPreferences1 = workPart.Annotations.Preferences.GetLineAndArrowPreferences();
            dimensionData1.SetLineAndArrowPreferences(lineAndArrowPreferences1);
            lineAndArrowPreferences1.Dispose();
            NXOpen.Annotations.LetteringPreferences letteringPreferences1;
            letteringPreferences1 = workPart.Annotations.Preferences.GetLetteringPreferences();
            dimensionData1.SetLetteringPreferences(letteringPreferences1);
            letteringPreferences1.Dispose();
            NXOpen.Annotations.UserSymbolPreferences userSymbolPreferences1;
            userSymbolPreferences1 = workPart.Annotations.NewUserSymbolPreferences(NXOpen.Annotations.UserSymbolPreferences.SizeType.ScaleAspectRatio, 1.0, 1.0);
            dimensionData1.SetUserSymbolPreferences(userSymbolPreferences1);
            userSymbolPreferences1.Dispose();
            NXOpen.Annotations.LinearTolerance linearTolerance1;
            linearTolerance1 = workPart.Annotations.Preferences.GetLinearTolerances();
            dimensionData1.SetLinearTolerance(linearTolerance1);
            linearTolerance1.Dispose();
            NXOpen.Annotations.AngularTolerance angularTolerance1;
            angularTolerance1 = workPart.Annotations.Preferences.GetAngularTolerances();
            NXOpen.Annotations.Value lowerToleranceDegrees1;
            lowerToleranceDegrees1.ItemValue = -0.1;
            Expression nullExpression = null;
            lowerToleranceDegrees1.ValueExpression = nullExpression;
            lowerToleranceDegrees1.ValuePrecision = 3;
            angularTolerance1.SetLowerToleranceDegrees(lowerToleranceDegrees1);
            NXOpen.Annotations.Value upperToleranceDegrees1;
            upperToleranceDegrees1.ItemValue = 0.1;
            upperToleranceDegrees1.ValueExpression = nullExpression;
            upperToleranceDegrees1.ValuePrecision = 3;
            angularTolerance1.SetUpperToleranceDegrees(upperToleranceDegrees1);
            dimensionData1.SetAngularTolerance(angularTolerance1);
            angularTolerance1.Dispose();
            NXOpen.Annotations.AppendedText appendedText1;
            appendedText1 = workPart.Annotations.NewAppendedText();
            String[] lines1 = new String[0];
            appendedText1.SetAboveText(lines1);
            String[] lines2 = new String[0];
            appendedText1.SetAfterText(lines2);
            String[] lines3 = new String[0];
            appendedText1.SetBeforeText(lines3);
            String[] lines4 = new String[0];
            appendedText1.SetBelowText(lines4);
            dimensionData1.SetAppendedText(appendedText1);
            appendedText1.Dispose();
            NXOpen.Annotations.PmiData pmiData1;
            pmiData1 = workPart.Annotations.NewPmiData();
            NXOpen.Annotations.BusinessModifier[] businessModifiers1 = new NXOpen.Annotations.BusinessModifier[0];
            pmiData1.SetBusinessModifiers(businessModifiers1);
            Xform xform1;
            xform1 = dimensionData1.GetInferredPlane(NXOpen.Annotations.PmiDefaultPlane.ModelView, NXOpen.Annotations.DimensionType.Parallel);
            Point3d origin1 = new Point3d(0.0, 0.0, 0.0);
            NXOpen.Annotations.PmiParallelDimension pmiParallelDimension1;
            pmiParallelDimension1 = workPart.Dimensions.CreatePmiParallelDimension(dimensionData1, pmiData1, xform1, origin1);
            pmiParallelDimension1.IsOriginCentered = true;
            markdim = pmiParallelDimension1;//赋值
            dimensionData1.Dispose();
            pmiData1.Dispose();
        }
        #endregion
        
        #region 径向标注（全部换为圆柱标注
        public void Cylindricaldimension(Face markFace, Point3d helppoint, out Dimension markDim)
        {
            Session theSession = Session.GetSession();
            Part workPart = theSession.Parts.Work;
            Part displayPart = theSession.Parts.Display;

            NXOpen.Annotations.DimensionData dimensionData1;
            dimensionData1 = workPart.Annotations.NewDimensionData();

            NXOpen.Annotations.Associativity associativity1;
            associativity1 = workPart.Annotations.NewAssociativity();

            associativity1.FirstObject = markFace;

            NXObject nullNXObject = null;
            associativity1.SecondObject = nullNXObject;

            associativity1.ObjectView = null;

            associativity1.PointOption = NXOpen.Annotations.AssociativityPointOption.None;

            associativity1.LineOption = NXOpen.Annotations.AssociativityLineOption.None;

            Point3d firstDefinitionPoint1 = new Point3d(0.0, 0.0, 0.0);
            associativity1.FirstDefinitionPoint = firstDefinitionPoint1;

            Point3d secondDefinitionPoint1 = new Point3d(0.0, 0.0, 0.0);
            associativity1.SecondDefinitionPoint = secondDefinitionPoint1;

            associativity1.Angle = 0.0;

            Point3d pickPoint1 = new Point3d(40.0, 0.0, -1.0);
            associativity1.PickPoint = pickPoint1;

            NXOpen.Annotations.Associativity[] associativity2 = new NXOpen.Annotations.Associativity[1];
            associativity2[0] = associativity1;
            dimensionData1.SetAssociativity(1, associativity2);

            associativity1.Dispose();
            NXOpen.Annotations.Associativity associativity3;
            associativity3 = workPart.Annotations.NewAssociativity();

            associativity3.FirstObject = markFace;

            associativity3.SecondObject = nullNXObject;

            associativity3.ObjectView = null;

            associativity3.PointOption = NXOpen.Annotations.AssociativityPointOption.None;

            associativity3.LineOption = NXOpen.Annotations.AssociativityLineOption.None;

            Point3d firstDefinitionPoint2 = new Point3d(0.0, 0.0, 0.0);
            associativity3.FirstDefinitionPoint = firstDefinitionPoint2;

            Point3d secondDefinitionPoint2 = new Point3d(0.0, 0.0, 0.0);
            associativity3.SecondDefinitionPoint = secondDefinitionPoint2;

            associativity3.Angle = 0.0;

            Point3d pickPoint2 = new Point3d(40.0, 0.0, 1.0);
            associativity3.PickPoint = pickPoint2;

            NXOpen.Annotations.Associativity[] associativity4 = new NXOpen.Annotations.Associativity[1];
            associativity4[0] = associativity3;
            dimensionData1.SetAssociativity(2, associativity4);

            associativity3.Dispose();
            NXOpen.Annotations.DimensionPreferences dimensionPreferences1;
            dimensionPreferences1 = workPart.Annotations.Preferences.GetDimensionPreferences();

            NXOpen.Annotations.OrdinateDimensionPreferences ordinateDimensionPreferences1;
            ordinateDimensionPreferences1 = dimensionPreferences1.GetOrdinateDimensionPreferences();

            ordinateDimensionPreferences1.Dispose();
            NXOpen.Annotations.ChamferDimensionPreferences chamferDimensionPreferences1;
            chamferDimensionPreferences1 = dimensionPreferences1.GetChamferDimensionPreferences();

            chamferDimensionPreferences1.Dispose();
            NXOpen.Annotations.NarrowDimensionPreferences narrowDimensionPreferences1;
            narrowDimensionPreferences1 = dimensionPreferences1.GetNarrowDimensionPreferences();

            narrowDimensionPreferences1.DimensionDisplayOption = NXOpen.Annotations.NarrowDisplayOption.None;

            dimensionPreferences1.SetNarrowDimensionPreferences(narrowDimensionPreferences1);

            narrowDimensionPreferences1.Dispose();
            NXOpen.Annotations.UnitsFormatPreferences unitsFormatPreferences1;
            unitsFormatPreferences1 = dimensionPreferences1.GetUnitsFormatPreferences();

            unitsFormatPreferences1.Dispose();
            NXOpen.Annotations.DiameterRadiusPreferences diameterRadiusPreferences1;
            diameterRadiusPreferences1 = dimensionPreferences1.GetDiameterRadiusPreferences();

            diameterRadiusPreferences1.Dispose();
            dimensionData1.SetDimensionPreferences(dimensionPreferences1);

            dimensionPreferences1.Dispose();
            NXOpen.Annotations.LineAndArrowPreferences lineAndArrowPreferences1;
            lineAndArrowPreferences1 = workPart.Annotations.Preferences.GetLineAndArrowPreferences();

            dimensionData1.SetLineAndArrowPreferences(lineAndArrowPreferences1);

            lineAndArrowPreferences1.Dispose();
            NXOpen.Annotations.LetteringPreferences letteringPreferences1;
            letteringPreferences1 = workPart.Annotations.Preferences.GetLetteringPreferences();

            dimensionData1.SetLetteringPreferences(letteringPreferences1);

            letteringPreferences1.Dispose();
            NXOpen.Annotations.UserSymbolPreferences userSymbolPreferences1;
            userSymbolPreferences1 = workPart.Annotations.NewUserSymbolPreferences(NXOpen.Annotations.UserSymbolPreferences.SizeType.ScaleAspectRatio, 1.0, 1.0);

            dimensionData1.SetUserSymbolPreferences(userSymbolPreferences1);

            userSymbolPreferences1.Dispose();
            NXOpen.Annotations.LinearTolerance linearTolerance1;
            linearTolerance1 = workPart.Annotations.Preferences.GetLinearTolerances();

            dimensionData1.SetLinearTolerance(linearTolerance1);

            linearTolerance1.Dispose();
            NXOpen.Annotations.AngularTolerance angularTolerance1;
            angularTolerance1 = workPart.Annotations.Preferences.GetAngularTolerances();

            NXOpen.Annotations.Value lowerToleranceDegrees1;
            lowerToleranceDegrees1.ItemValue = -0.1;
            Expression nullExpression = null;
            lowerToleranceDegrees1.ValueExpression = nullExpression;
            lowerToleranceDegrees1.ValuePrecision = 3;
            angularTolerance1.SetLowerToleranceDegrees(lowerToleranceDegrees1);

            NXOpen.Annotations.Value upperToleranceDegrees1;
            upperToleranceDegrees1.ItemValue = 0.1;
            upperToleranceDegrees1.ValueExpression = nullExpression;
            upperToleranceDegrees1.ValuePrecision = 3;
            angularTolerance1.SetUpperToleranceDegrees(upperToleranceDegrees1);

            dimensionData1.SetAngularTolerance(angularTolerance1);

            angularTolerance1.Dispose();
            NXOpen.Annotations.AppendedText appendedText1;
            appendedText1 = workPart.Annotations.NewAppendedText();

            String[] lines1 = new String[0];
            appendedText1.SetAboveText(lines1);

            String[] lines2 = new String[0];
            appendedText1.SetAfterText(lines2);

            String[] lines3 = new String[0];
            appendedText1.SetBeforeText(lines3);

            String[] lines4 = new String[0];
            appendedText1.SetBelowText(lines4);

            dimensionData1.SetAppendedText(appendedText1);

            appendedText1.Dispose();
            NXOpen.Annotations.PmiData pmiData1;
            pmiData1 = workPart.Annotations.NewPmiData();

            NXOpen.Annotations.BusinessModifier[] businessModifiers1 = new NXOpen.Annotations.BusinessModifier[0];
            pmiData1.SetBusinessModifiers(businessModifiers1);

            Xform xform1;
            xform1 = dimensionData1.GetInferredPlane(NXOpen.Annotations.PmiDefaultPlane.ModelView, NXOpen.Annotations.DimensionType.Cylindrical);

            Point3d origin1 = new Point3d(0.0, 0.0, 0.0);
            NXOpen.Annotations.PmiCylindricalDimension pmiCylindricalDimension1;
            pmiCylindricalDimension1 = workPart.Dimensions.CreatePmiCylindricalDimension(dimensionData1, pmiData1, xform1, origin1);
            markDim = pmiCylindricalDimension1;
            dimensionData1.Dispose();
            pmiData1.Dispose();
        }
        #endregion

        #region 对圆弧面而非圆柱面的轴段进行标注
        //按照手动标注的步骤一步步的来
        #region 针对圆弧面的标注，建立基准平面，需要获取工作部件
        public void createdatadum(Face datumface, out DatumPlane mark_plane)
        {
            //到时还是要改成装配
            Part workPart = theSession.Parts.Work;
            Part displayPart = theSession.Parts.Display;
            NXOpen.Features.Feature nullFeatures_Feature = null;
            NXOpen.Features.DatumPlaneBuilder datumPlaneBuilder1;
            datumPlaneBuilder1 = workPart.Features.CreateDatumPlaneBuilder(nullFeatures_Feature);
            //得到要穿过的平面的对象
            Plane plane1;
            plane1 = datumPlaneBuilder1.GetPlane();
            Point3d coordinates1 = new Point3d(0.0, 0.0, 0.0);
            Point point1;
            point1 = workPart.Points.CreatePoint(coordinates1);
            plane1.SetUpdateOption(NXOpen.SmartObject.UpdateOption.WithinModeling);
            plane1.SetMethod(NXOpen.PlaneTypes.MethodType.Coincident);
            //获得穿过的面
            NXObject[] geom1 = new NXObject[1];
            Face face1 = datumface;
            geom1[0] = face1;
            plane1.SetGeometry(geom1);
            plane1.SetAlternate(NXOpen.PlaneTypes.AlternateType.One);
            plane1.Evaluate();
            bool flip1;
            flip1 = plane1.Flip;
            NXOpen.Features.Feature feature1;
            feature1 = datumPlaneBuilder1.CommitFeature();
            //这个不能删
            NXOpen.Features.DatumPlaneFeature datumPlaneFeature1 = (NXOpen.Features.DatumPlaneFeature)feature1;
            DatumPlane datumPlane1;
            datumPlane1 = datumPlaneFeature1.DatumPlane;
            mark_plane = datumPlane1;//输出一个基准平面
            datumPlaneBuilder1.Destroy();
        }
        #endregion 

        #region 针对圆弧面的标注，建立交线，需要获取工作部件
        public void createlinkcurve(DatumPlane datumplane, Face datumface, out IntersectionCurve mark_curve)//输出为交线
        {
            //到时还是要改成装配
            Part workPart = theSession.Parts.Work;
            Part displayPart = theSession.Parts.Display;
            NXOpen.Features.Feature nullFeatures_Feature = null;
            NXOpen.Features.IntersectionCurveBuilder intersectionCurveBuilder1;
            intersectionCurveBuilder1 = workPart.Features.CreateIntersectionCurveBuilder(nullFeatures_Feature);
            //得到基准平面
            DatumPlane[] faces1 = new DatumPlane[1];
            faces1[0] = datumplane;
            FaceDumbRule faceDumbRule1;
            faceDumbRule1 = workPart.ScRuleFactory.CreateRuleFaceDatum(faces1);
            SelectionIntentRule[] rules1 = new SelectionIntentRule[1];
            rules1[0] = faceDumbRule1;
            intersectionCurveBuilder1.FirstFace.ReplaceRules(rules1, false);
            TaggedObject[] objects1 = new TaggedObject[1];
            objects1[0] =datumplane;
            bool added1;
            added1 = intersectionCurveBuilder1.FirstSet.Add(objects1);
            //得到平面
            Face face1 = datumface;
            Face[] boundaryFaces1 = new Face[0];
            FaceTangentRule faceTangentRule1;
            faceTangentRule1 = workPart.ScRuleFactory.CreateRuleFaceTangent(face1, boundaryFaces1);
            SelectionIntentRule[] rules2 = new SelectionIntentRule[1];
            rules2[0] = faceTangentRule1;
            intersectionCurveBuilder1.SecondFace.ReplaceRules(rules2, false);
            TaggedObject[] objects2 = new TaggedObject[1];
            objects2[0] = face1;
            bool added2;
            added2 = intersectionCurveBuilder1.SecondSet.Add(objects2);
            intersectionCurveBuilder1.Tolerance = 0.0001;
            NXObject nXObject1;
            nXObject1 = intersectionCurveBuilder1.Commit();
            mark_curve = (IntersectionCurve)nXObject1;
            intersectionCurveBuilder1.Destroy();
        }
        #endregion

        #region 标注圆弧面的半径，需要获取工作部件
        public void createArcDimension(IntersectionCurve mark_curve)
        {
            //到时还是要改成装配
            Part workPart = theSession.Parts.Work;
            Part displayPart = theSession.Parts.Display;
            NXOpen.Annotations.DimensionData dimensionData1;
            dimensionData1 = workPart.Annotations.NewDimensionData();
            NXOpen.Annotations.Associativity associativity1;
            associativity1 = workPart.Annotations.NewAssociativity();
            Arc arc1 = (Arc)mark_curve.FindObject("CURVE 1");
            associativity1.FirstObject = arc1;
            NXObject nullNXObject = null;
            associativity1.SecondObject = nullNXObject;
            associativity1.ObjectView = null;//老规矩，改成null
            associativity1.PointOption = NXOpen.Annotations.AssociativityPointOption.None;
            associativity1.LineOption = NXOpen.Annotations.AssociativityLineOption.None;
            Point3d firstDefinitionPoint1 = new Point3d(0.0, 0.0, 0.0);
            associativity1.FirstDefinitionPoint = firstDefinitionPoint1;
            Point3d secondDefinitionPoint1 = new Point3d(0.0, 0.0, 0.0);
            associativity1.SecondDefinitionPoint = secondDefinitionPoint1;
            associativity1.Angle = 0.0;
            Point3d pickPoint1 = new Point3d(100.523014356708, 8.33644483071772, 8.88178419700125e-016);
            associativity1.PickPoint = pickPoint1;
            NXOpen.Annotations.Associativity[] associativity2 = new NXOpen.Annotations.Associativity[1];
            associativity2[0] = associativity1;
            dimensionData1.SetAssociativity(1, associativity2);
            associativity1.Dispose();
            NXOpen.Annotations.DimensionPreferences dimensionPreferences1;
            dimensionPreferences1 = workPart.Annotations.Preferences.GetDimensionPreferences();
            NXOpen.Annotations.OrdinateDimensionPreferences ordinateDimensionPreferences1;
            ordinateDimensionPreferences1 = dimensionPreferences1.GetOrdinateDimensionPreferences();
            ordinateDimensionPreferences1.Dispose();
            NXOpen.Annotations.ChamferDimensionPreferences chamferDimensionPreferences1;
            chamferDimensionPreferences1 = dimensionPreferences1.GetChamferDimensionPreferences();
            chamferDimensionPreferences1.Dispose();
            NXOpen.Annotations.NarrowDimensionPreferences narrowDimensionPreferences1;
            narrowDimensionPreferences1 = dimensionPreferences1.GetNarrowDimensionPreferences();
            narrowDimensionPreferences1.DimensionDisplayOption = NXOpen.Annotations.NarrowDisplayOption.None;
            dimensionPreferences1.SetNarrowDimensionPreferences(narrowDimensionPreferences1);
            narrowDimensionPreferences1.Dispose();
            NXOpen.Annotations.UnitsFormatPreferences unitsFormatPreferences1;
            unitsFormatPreferences1 = dimensionPreferences1.GetUnitsFormatPreferences();
            unitsFormatPreferences1.Dispose();
            NXOpen.Annotations.DiameterRadiusPreferences diameterRadiusPreferences1;
            diameterRadiusPreferences1 = dimensionPreferences1.GetDiameterRadiusPreferences();
            diameterRadiusPreferences1.Dispose();
            dimensionData1.SetDimensionPreferences(dimensionPreferences1);
            dimensionPreferences1.Dispose();
            NXOpen.Annotations.LineAndArrowPreferences lineAndArrowPreferences1;
            lineAndArrowPreferences1 = workPart.Annotations.Preferences.GetLineAndArrowPreferences();
            dimensionData1.SetLineAndArrowPreferences(lineAndArrowPreferences1);
            lineAndArrowPreferences1.Dispose();
            NXOpen.Annotations.LetteringPreferences letteringPreferences1;
            letteringPreferences1 = workPart.Annotations.Preferences.GetLetteringPreferences();
            dimensionData1.SetLetteringPreferences(letteringPreferences1);
            letteringPreferences1.Dispose();
            NXOpen.Annotations.UserSymbolPreferences userSymbolPreferences1;
            userSymbolPreferences1 = workPart.Annotations.NewUserSymbolPreferences(NXOpen.Annotations.UserSymbolPreferences.SizeType.ScaleAspectRatio, 1.0, 1.0);
            dimensionData1.SetUserSymbolPreferences(userSymbolPreferences1);
            userSymbolPreferences1.Dispose();
            NXOpen.Annotations.LinearTolerance linearTolerance1;
            linearTolerance1 = workPart.Annotations.Preferences.GetLinearTolerances();
            dimensionData1.SetLinearTolerance(linearTolerance1);
            linearTolerance1.Dispose();
            NXOpen.Annotations.AngularTolerance angularTolerance1;
            angularTolerance1 = workPart.Annotations.Preferences.GetAngularTolerances();
            NXOpen.Annotations.Value lowerToleranceDegrees1;
            lowerToleranceDegrees1.ItemValue = -0.1;
            Expression nullExpression = null;
            lowerToleranceDegrees1.ValueExpression = nullExpression;
            lowerToleranceDegrees1.ValuePrecision = 3;
            angularTolerance1.SetLowerToleranceDegrees(lowerToleranceDegrees1);
            NXOpen.Annotations.Value upperToleranceDegrees1;
            upperToleranceDegrees1.ItemValue = 0.1;
            upperToleranceDegrees1.ValueExpression = nullExpression;
            upperToleranceDegrees1.ValuePrecision = 3;
            angularTolerance1.SetUpperToleranceDegrees(upperToleranceDegrees1);
            dimensionData1.SetAngularTolerance(angularTolerance1);
            angularTolerance1.Dispose();
            NXOpen.Annotations.AppendedText appendedText1;
            appendedText1 = workPart.Annotations.NewAppendedText();
            String[] lines1 = new String[0];
            appendedText1.SetAboveText(lines1);
            String[] lines2 = new String[0];
            appendedText1.SetAfterText(lines2);
            String[] lines3 = new String[0];
            appendedText1.SetBeforeText(lines3);
            String[] lines4 = new String[0];
            appendedText1.SetBelowText(lines4);
            dimensionData1.SetAppendedText(appendedText1);
            appendedText1.Dispose();
            NXOpen.Annotations.PmiData pmiData1;
            pmiData1 = workPart.Annotations.NewPmiData();
            NXOpen.Annotations.BusinessModifier[] businessModifiers1 = new NXOpen.Annotations.BusinessModifier[0];
            pmiData1.SetBusinessModifiers(businessModifiers1);
            Xform xform1;
            xform1 = dimensionData1.GetInferredPlane(NXOpen.Annotations.PmiDefaultPlane.ModelView, NXOpen.Annotations.DimensionType.Radius);
            Point3d origin1 = new Point3d(0.0, 0.0, 0.0);
            NXOpen.Annotations.PmiRadiusDimension pmiRadiusDimension1;
            pmiRadiusDimension1 = workPart.Dimensions.CreatePmiRadiusDimension(dimensionData1, NXOpen.Annotations.RadiusDimensionType.NotToCenter, pmiData1, xform1, origin1);
            dimensionData1.Dispose();
            pmiData1.Dispose();
            pmiRadiusDimension1.IsOriginCentered = true;
        }
        #endregion 
        #endregion 

        #region 冗余的检查
        public void CheckOverDimension(Face checkface1,Face checkface2,List<Face>Checkface,out bool check)
        {
            check = false;
            if ((Checkface.Contains(checkface1) == true) & (Checkface.Contains(checkface2) == true))
            {
                check = true;
            }
        }
        #endregion 

        #region 将属性邻接图存储到XML中
        public void InputXml(string name, List<int[]> neigh_Face,XmlDocument all)
        {
            XmlDocument xml = all;
            XmlNode root = xml.SelectSingleNode("零件名及其零件属性图");
            XmlElement rootEle = xml.CreateElement(name);//当前部件名字
            root.AppendChild(rootEle);
            foreach (int[] num in neigh_Face)
            {
                XmlElement childEle = xml.CreateElement("属性邻接图");
                string content="[";
                for (int i = 0; i < num.Length; i++)
                {
                    content = content + num[i].ToString()+" ";
                }
                content = content + "]";
                childEle.InnerText = content;
                rootEle.AppendChild(childEle);
            }
            xml.Save("d://属性邻接图.xml");
        }
        #endregion 

        //public void Cylindricaldimension(Face face1, out Dimension dim)
        //{
        //    Session theSession = Session.GetSession();
        //    Part workPart = theSession.Parts.Work;
        //    Part displayPart = theSession.Parts.Display;

        //    NXOpen.Annotations.DimensionData dimensionData1;
        //    dimensionData1 = workPart.Annotations.NewDimensionData();

        //    NXOpen.Annotations.Associativity associativity1;
        //    associativity1 = workPart.Annotations.NewAssociativity();

        //    associativity1.FirstObject = face1;

        //    NXObject nullNXObject = null;
        //    associativity1.SecondObject = nullNXObject;

        //    associativity1.ObjectView = workPart.ModelingViews.WorkView;

        //    associativity1.PointOption = NXOpen.Annotations.AssociativityPointOption.None;

        //    associativity1.LineOption = NXOpen.Annotations.AssociativityLineOption.None;

        //    Point3d firstDefinitionPoint1 = new Point3d(0.0, 0.0, 0.0);
        //    associativity1.FirstDefinitionPoint = firstDefinitionPoint1;

        //    Point3d secondDefinitionPoint1 = new Point3d(0.0, 0.0, 0.0);
        //    associativity1.SecondDefinitionPoint = secondDefinitionPoint1;

        //    associativity1.Angle = 0.0;

        //    Point3d pickPoint1 = new Point3d(40.0, 0.0, -1.0);
        //    associativity1.PickPoint = pickPoint1;

        //    NXOpen.Annotations.Associativity[] associativity2 = new NXOpen.Annotations.Associativity[1];
        //    associativity2[0] = associativity1;
        //    dimensionData1.SetAssociativity(1, associativity2);

        //    associativity1.Dispose();
        //    NXOpen.Annotations.Associativity associativity3;
        //    associativity3 = workPart.Annotations.NewAssociativity();

        //    associativity3.FirstObject = face1;

        //    associativity3.SecondObject = nullNXObject;

        //    associativity3.ObjectView = workPart.ModelingViews.WorkView;

        //    associativity3.PointOption = NXOpen.Annotations.AssociativityPointOption.None;

        //    associativity3.LineOption = NXOpen.Annotations.AssociativityLineOption.None;

        //    Point3d firstDefinitionPoint2 = new Point3d(0.0, 0.0, 0.0);
        //    associativity3.FirstDefinitionPoint = firstDefinitionPoint2;

        //    Point3d secondDefinitionPoint2 = new Point3d(0.0, 0.0, 0.0);
        //    associativity3.SecondDefinitionPoint = secondDefinitionPoint2;

        //    associativity3.Angle = 0.0;

        //    Point3d pickPoint2 = new Point3d(40.0, 0.0, 1.0);
        //    associativity3.PickPoint = pickPoint2;

        //    NXOpen.Annotations.Associativity[] associativity4 = new NXOpen.Annotations.Associativity[1];
        //    associativity4[0] = associativity3;
        //    dimensionData1.SetAssociativity(2, associativity4);

        //    associativity3.Dispose();
        //    NXOpen.Annotations.DimensionPreferences dimensionPreferences1;
        //    dimensionPreferences1 = workPart.Annotations.Preferences.GetDimensionPreferences();

        //    NXOpen.Annotations.OrdinateDimensionPreferences ordinateDimensionPreferences1;
        //    ordinateDimensionPreferences1 = dimensionPreferences1.GetOrdinateDimensionPreferences();

        //    ordinateDimensionPreferences1.Dispose();
        //    NXOpen.Annotations.ChamferDimensionPreferences chamferDimensionPreferences1;
        //    chamferDimensionPreferences1 = dimensionPreferences1.GetChamferDimensionPreferences();

        //    chamferDimensionPreferences1.Dispose();
        //    NXOpen.Annotations.NarrowDimensionPreferences narrowDimensionPreferences1;
        //    narrowDimensionPreferences1 = dimensionPreferences1.GetNarrowDimensionPreferences();

        //    narrowDimensionPreferences1.DimensionDisplayOption = NXOpen.Annotations.NarrowDisplayOption.None;

        //    dimensionPreferences1.SetNarrowDimensionPreferences(narrowDimensionPreferences1);

        //    narrowDimensionPreferences1.Dispose();
        //    NXOpen.Annotations.UnitsFormatPreferences unitsFormatPreferences1;
        //    unitsFormatPreferences1 = dimensionPreferences1.GetUnitsFormatPreferences();

        //    unitsFormatPreferences1.Dispose();
        //    NXOpen.Annotations.DiameterRadiusPreferences diameterRadiusPreferences1;
        //    diameterRadiusPreferences1 = dimensionPreferences1.GetDiameterRadiusPreferences();

        //    diameterRadiusPreferences1.Dispose();
        //    dimensionData1.SetDimensionPreferences(dimensionPreferences1);

        //    dimensionPreferences1.Dispose();
        //    NXOpen.Annotations.LineAndArrowPreferences lineAndArrowPreferences1;
        //    lineAndArrowPreferences1 = workPart.Annotations.Preferences.GetLineAndArrowPreferences();

        //    dimensionData1.SetLineAndArrowPreferences(lineAndArrowPreferences1);

        //    lineAndArrowPreferences1.Dispose();
        //    NXOpen.Annotations.LetteringPreferences letteringPreferences1;
        //    letteringPreferences1 = workPart.Annotations.Preferences.GetLetteringPreferences();

        //    dimensionData1.SetLetteringPreferences(letteringPreferences1);

        //    letteringPreferences1.Dispose();
        //    NXOpen.Annotations.UserSymbolPreferences userSymbolPreferences1;
        //    userSymbolPreferences1 = workPart.Annotations.NewUserSymbolPreferences(NXOpen.Annotations.UserSymbolPreferences.SizeType.ScaleAspectRatio, 1.0, 1.0);

        //    dimensionData1.SetUserSymbolPreferences(userSymbolPreferences1);

        //    userSymbolPreferences1.Dispose();
        //    NXOpen.Annotations.LinearTolerance linearTolerance1;
        //    linearTolerance1 = workPart.Annotations.Preferences.GetLinearTolerances();

        //    dimensionData1.SetLinearTolerance(linearTolerance1);

        //    linearTolerance1.Dispose();
        //    NXOpen.Annotations.AngularTolerance angularTolerance1;
        //    angularTolerance1 = workPart.Annotations.Preferences.GetAngularTolerances();

        //    NXOpen.Annotations.Value lowerToleranceDegrees1;
        //    lowerToleranceDegrees1.ItemValue = -0.1;
        //    Expression nullExpression = null;
        //    lowerToleranceDegrees1.ValueExpression = nullExpression;
        //    lowerToleranceDegrees1.ValuePrecision = 3;
        //    angularTolerance1.SetLowerToleranceDegrees(lowerToleranceDegrees1);

        //    NXOpen.Annotations.Value upperToleranceDegrees1;
        //    upperToleranceDegrees1.ItemValue = 0.1;
        //    upperToleranceDegrees1.ValueExpression = nullExpression;
        //    upperToleranceDegrees1.ValuePrecision = 3;
        //    angularTolerance1.SetUpperToleranceDegrees(upperToleranceDegrees1);

        //    dimensionData1.SetAngularTolerance(angularTolerance1);

        //    angularTolerance1.Dispose();
        //    NXOpen.Annotations.AppendedText appendedText1;
        //    appendedText1 = workPart.Annotations.NewAppendedText();

        //    String[] lines1 = new String[0];
        //    appendedText1.SetAboveText(lines1);

        //    String[] lines2 = new String[0];
        //    appendedText1.SetAfterText(lines2);

        //    String[] lines3 = new String[0];
        //    appendedText1.SetBeforeText(lines3);

        //    String[] lines4 = new String[0];
        //    appendedText1.SetBelowText(lines4);

        //    dimensionData1.SetAppendedText(appendedText1);

        //    appendedText1.Dispose();
        //    NXOpen.Annotations.PmiData pmiData1;
        //    pmiData1 = workPart.Annotations.NewPmiData();

        //    NXOpen.Annotations.BusinessModifier[] businessModifiers1 = new NXOpen.Annotations.BusinessModifier[0];
        //    pmiData1.SetBusinessModifiers(businessModifiers1);

        //    Xform xform1;
        //    xform1 = dimensionData1.GetInferredPlane(NXOpen.Annotations.PmiDefaultPlane.ModelView, NXOpen.Annotations.DimensionType.Cylindrical);

        //    Point3d origin1 = new Point3d(0.0, 0.0, 0.0);
        //    NXOpen.Annotations.PmiCylindricalDimension pmiCylindricalDimension1;
        //    pmiCylindricalDimension1 = workPart.Dimensions.CreatePmiCylindricalDimension(dimensionData1, pmiData1, xform1, origin1);
        //    dim = pmiCylindricalDimension1;
        //    dimensionData1.Dispose();
        //    pmiData1.Dispose();
        //}

        #region 对退刀槽进行检查并进行标注（暂时不用
        //public void CheckRetractor(Face[] all_face)
        //{
        //    #region 检查退刀槽
        //    foreach (Face check_face in all_face)
        //    {
        //        List<Face> LastCompareFace = new List<Face>();
        //        List<Face> compare_face1 = new List<Face>();
        //        Edge[] check_edge = check_face.GetEdges();
        //        if (check_edge.Length == 2)//首先要有两个边
        //        {
        //            Edge edge1 = check_edge[0];
        //            Edge edge2 = check_edge[1];
        //            Face[] face1 = edge1.GetFaces();
        //            Face[] face2 = edge2.GetFaces();
        //            foreach (Face help_face1 in face1)
        //            {
        //                int temp = 0;
        //                foreach (Face help_face2 in face2)
        //                {
        //                    temp = temp + 1;
        //                    if (help_face1 == help_face2)
        //                    {
        //                        break;
        //                    }
        //                    if (temp == 2)
        //                    {
        //                        compare_face1.Add(help_face1);
        //                    }
        //                }
        //            }
        //            foreach (Face help_face2 in face2)
        //            {
        //                int temp = 0;
        //                foreach (Face help_face1 in face1)
        //                {
        //                    temp = temp + 1;
        //                    if (help_face2 == help_face1)
        //                    {
        //                        break;
        //                    }
        //                    if (temp == 2)
        //                    {
        //                        compare_face1.Add(help_face2);
        //                    }
        //                }
        //            }
        //        }
        //        Face[] help_face = compare_face1.ToArray();
        //        if (help_face.Length == 2)
        //        {
        //            Face face3 = help_face[0];
        //            Face face4 = help_face[1];
        //            List<Face> compare_face3 = new List<Face>();
        //            Edge[] face3_edge = face3.GetEdges();
        //            Edge[] face4_edge = face4.GetEdges();
        //            //用来对比的素材
        //            //首先检查face3是否有两个边
        //            if (face3_edge.Length == 2)
        //            {
        //                Face[] help1_face3 = face3_edge[0].GetFaces();
        //                Face[] help2_face3 = face3_edge[1].GetFaces();
        //                foreach (Face help_face3_1 in help1_face3)
        //                {
        //                    int temp = 0;
        //                    foreach (Face help_face3_2 in help2_face3)
        //                    {
        //                        temp = temp + 1;
        //                        if (help_face3_1 == help_face3_2)
        //                        {
        //                            break;
        //                        }
        //                        if (temp == 2)
        //                        {
        //                            compare_face3.Add(help_face3_1);
        //                        }
        //                    }
        //                }
        //                foreach (Face help_face3_2 in help2_face3)
        //                {
        //                    int temp = 0;
        //                    foreach (Face help_face3_1 in help1_face3)
        //                    {
        //                        temp = temp + 1;
        //                        if (help_face3_2 == help_face3_1)
        //                        {
        //                            break;
        //                        }
        //                        if (temp == 2)
        //                        {
        //                            compare_face3.Add(help_face3_2);
        //                        }
        //                    }
        //                }
        //            }
        //            //检查face4
        //            List<Face> compare_face4 = new List<Face>();
        //            if (face4_edge.Length == 2)
        //            {
        //                Face[] help1_face4 = face4_edge[0].GetFaces();
        //                Face[] help2_face4 = face4_edge[1].GetFaces();
        //                foreach (Face help_face4_1 in help1_face4)
        //                {
        //                    int temp = 0;
        //                    foreach (Face help_face4_2 in help2_face4)
        //                    {
        //                        temp = temp + 1;
        //                        if (help_face4_1 == help_face4_2)
        //                        {
        //                            break;
        //                        }
        //                        if (temp == 2)
        //                        {
        //                            compare_face4.Add(help_face4_1);
        //                        }
        //                    }
        //                }
        //                foreach (Face help_face4_2 in help2_face4)
        //                {
        //                    int temp = 0;
        //                    foreach (Face help_face4_1 in help1_face4)
        //                    {
        //                        temp = temp + 1;
        //                        if (help_face4_2 == help_face4_1)
        //                        {
        //                            break;
        //                        }
        //                        if (temp == 2)
        //                        {
        //                            compare_face4.Add(help_face4_2);
        //                        }
        //                    }
        //                }
        //            }
        //            Face[] compareface3 = compare_face3.ToArray();
        //            Face[] compareface4 = compare_face4.ToArray();
        //            if ((compareface3.Length == compareface4.Length) & (compareface4.Length == 2))
        //            {
        //                foreach (Face lastface1 in compareface3)
        //                {
        //                    int temp = 0;
        //                    foreach (Face lastface2 in compareface4)
        //                    {
        //                        temp = temp + 1;
        //                        if (lastface1 == lastface2)
        //                        {
        //                            break;
        //                        }
        //                        if (temp == 2)
        //                        {
        //                            LastCompareFace.Add(lastface1);
        //                        }
        //                    }
        //                }
        //                foreach (Face lastface2 in compareface4)
        //                {
        //                    int temp = 0;
        //                    foreach (Face lastface1 in compareface3)
        //                    {
        //                        temp = temp + 1;
        //                        if (lastface2 == lastface1)
        //                        {
        //                            break;
        //                        }
        //                        if (temp == 2)
        //                        {
        //                            LastCompareFace.Add(lastface2);
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //        Face[] helpfacelast = LastCompareFace.ToArray();
        //        if (helpfacelast.Length != 0)
        //        {
        //            if ((helpfacelast.Length == 2) & (check_face.SolidFaceType.ToString() == "Cylindrical") & (helpfacelast[0].SolidFaceType.ToString() == "Cylindrical") & (helpfacelast[1].SolidFaceType.ToString() == "Cylindrical"))
        //            {
        //                Dimension check_face_dim;
        //                Point3d helppoint = new Point3d(0, 0, 0);
        //                Cylindricaldimension(check_face, helppoint, out check_face_dim);
        //                Dimension face1_dim;
        //                Dimension face2_dim;
        //                Cylindricaldimension(helpfacelast[0], helppoint, out face1_dim);
        //                Cylindricaldimension(helpfacelast[1], helppoint, out face2_dim);
        //                if ((check_face_dim.ComputedSize < face2_dim.ComputedSize) & (check_face_dim.ComputedSize < face1_dim.ComputedSize))
        //                {
        //                    Edge[] all_mark_edge = check_face.GetEdges();
        //                    Edge mark1 = all_mark_edge[0];
        //                    Edge mark2 = all_mark_edge[1];
        //                    Dimension markdim;
        //                    createaxisdimension(mark1, mark2, out markdim);
        //                }
        //                NXObject[] deleteobject = new NXObject[3];
        //                deleteobject[0] = face1_dim;
        //                deleteobject[1] = face2_dim;
        //                deleteobject[2] = check_face_dim;
        //                DeleteObject(deleteobject);
        //            }
        //        }
        //    }
        //    #endregion
        //}
        #endregion 
    }
}