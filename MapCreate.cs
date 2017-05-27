using System.ComponentModel;
using System.Data;
using System.Text;
using System.Windows.Forms;
using NXOpen;
using NXOpenUI;
using NXOpen.UF;
using NXOpen.Features;
using NXOpen.GeometricUtilities;
using NXOpen.Annotations;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NXFunctions;
using System.Xml;

namespace MapWindows
{
    public partial class MapCreate : Form
    {
        private NXOpen.UF.UFSession theUFSession = NXOpen.UF.UFSession.GetUFSession();//这是整体维度
        private static Session theSession = Session.GetSession();//获得当前的
        private static NxFuntion NXFun = new NxFuntion();//方法都存在这里头
        private static UI theUI = null;
        public MapCreate()
        {
            InitializeComponent();
        }

        private void MapCreate_Load(object sender, EventArgs e)
        {

        }

        private void button3_Click(object sender, EventArgs e)//尺寸映射，成功将尺寸放到不同的视图中
        {
            #region 获取部件中各个part的名字
            int num_part;
            List<string> part_name = new List<string>();
            List<Tag> part_tag = new List<Tag>();
            //获得部件的个数
            num_part = theUFSession.Part.AskNumParts();
            //得到part的tag集
            for (int help_num_part = 0; help_num_part < num_part; help_num_part++)
            {
                part_tag.Add(theUFSession.Part.AskNthPart(help_num_part));
            }
            //得到part的name集
            foreach (Tag help_part in part_tag)
            {
                string help_name;
                theUFSession.Part.AskPartName(help_part, out help_name);
                Part help_part2 =(Part)theSession.Parts.FindObject(help_name);
                help_name = help_part2.JournalIdentifier;
                part_name.Add(help_name);//不再是路径了,直接是表中显示的名字，同时，findobject同样可以应用于这个非路径的名字
            }
            #endregion
            //获得整体
            string mother = null;
            foreach (string name in part_name)
            {
                if (name.Contains("Process") == true)
                {
                    mother = name;
                    break;
                }
            }

            string[] get_name=part_name.ToArray();
            for (int i = 0; i < (get_name.Length-1);i++)
            {
                if ((get_name[i].Contains("part") == true) || (get_name[i].Contains("blank") == true) || (get_name[i].Contains("Process") == true))
                {
                }
                else
                {
                    Part start_part = null;
                    Part link_part = null; 
                    Part map_part = null;
                    List<Face> Markface = new List<Face>();
                    List<double> Markfacepoint = new List<double>();
                    List<Face> Mapface = new List<Face>();
                    List<double> Mapfacepoint = new List<double>();
                    string start_name = get_name[i];//暂时没发现哪儿有问题
                    string link_name = get_name[i+1];
                    start_part = (Part)theSession.Parts.FindObject(start_name);
                    link_part = (Part)theSession.Parts.FindObject(link_name);
                    List<Face> Mark_sort_face1 = new List<Face>();

                    NXFun.GetMarkFace(start_part,link_part, out Mark_sort_face1);
                    //恢复装配序列
                    PartLoadStatus partLoadStatus2;
                    NXOpen.PartCollection.SdpsStatus status1;
                    Part part1 = (Part)theSession.Parts.FindObject(mother);
                    status1 = theSession.Parts.SetDisplay(part1, true, true, out partLoadStatus2);
                    partLoadStatus2.Dispose();

                    Face[] Mark_help_face = Mark_sort_face1.ToArray();
                    NXFun.GetFaceWithPoint_Finish(Mark_help_face, out Markface, out Markfacepoint);
                    //获得标注的平面和标注平面对应点
                    #region 精加工
                    if (start_name.Contains("finish") == true)
                    {
                        #region 寻找映射面
                        foreach (string search_name in get_name)
                        {
                            if (search_name.Contains("part") == true)//精加工直接与零件图纸进行标注
                            {
                                map_part = (Part)theSession.Parts.FindObject(search_name);
                                theUFSession.Assem.SetWorkPart(map_part.Tag);
                                Part workPart = theSession.Parts.Work;
                                Body[] mapbody = workPart.Bodies.ToArray();
                                Body map_body = mapbody[0];
                                Face[] map_face = map_body.GetFaces();
                                NXFun.GetFaceWithPoint_Finish(map_face, out Mapface, out Mapfacepoint);
                                break;
                            }
                        }
                        #endregion

                        #region 添加公共面
                        //如果当前工序模型和映射模型有相同的面，那么我们也要加到标注面里头去，但不能全加，因此要利用那个loc1和loc2
                        //对loc1和loc2对应的平面进行筛选，如果2个都不在原来的里头，那么就不标
                        List<Face> MarkfaceTest = new List<Face>();
                        List<double> MarkfaceTestpoint = new List<double>();
                        theUFSession.Assem.SetWorkPart(start_part.Tag);
                        Part TestPart = theSession.Parts.Work;
                        Body[] Testbodies = TestPart.Bodies.ToArray();
                        Body testbody = Testbodies[0];
                        Face[] test_face = testbody.GetFaces();
                        //直接继承之前的，我们可以发现肯定不会为0，所以就不用考虑加上temp了。
                        MarkfaceTest.AddRange(Markface);//用来对比
                        MarkfaceTestpoint.AddRange(Markfacepoint);//用来对比
                        foreach (Face testface in test_face)
                        {
                            double[] point_num_get = MarkfaceTestpoint.ToArray();
                            int num = point_num_get.Length;//得到点的个数，从而应对如果现在判断的点是最小的
                            if (num == 0)
                            {
                                break;
                            }
                            int type;
                            double[] point = new double[6];
                            double[] dir = new double[5];
                            double[] box = new double[6];
                            double radius;
                            double rad_data;
                            int norm_dir;
                            theUFSession.Modl.AskFaceData(testface.Tag, out type, point, dir, box, out radius, out rad_data, out norm_dir);
                            if (type == 22)
                            {
                                if (Mapfacepoint.Contains(point[0]) == true)//即当前所选择的面在映射尺寸那儿也有
                                {
                                    int index = 0;
                                    foreach (double check_point in MarkfaceTestpoint)
                                    {
                                        if ((check_point < point[0]) & (MarkfaceTestpoint.Contains(point[0]) == false))
                                        {
                                            MarkfaceTestpoint.Insert(index, point[0]);
                                            MarkfaceTest.Insert(index, testface);
                                            break;
                                        }
                                        index = index + 1;
                                        if ((index == num) & (MarkfaceTestpoint.Contains(point[0]) == false))
                                        {
                                            MarkfaceTestpoint.Add(point[0]);
                                            MarkfaceTest.Add(testface);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        #endregion

                        #region 开始映射
                        //如果是从右边开始
                        if (start_name.Contains("right") == true)
                        {
                            #region 开始找尺寸，这是对应从大到小，从右边开始
                            theUFSession.Assem.SetWorkPart(map_part.Tag);
                            Part workPartMark = theSession.Parts.Work;
                            Dimension[] map_dimension = workPartMark.Dimensions.ToArray();//所有尺寸
                            Face[] mark_face_in_target = Markface.ToArray();
                            List<Face> Over_check_Face = new List<Face>();
                            int mark_face_num = mark_face_in_target.Length;
                            theUFSession.Assem.SetWorkPart(start_part.Tag);
                            workPartMark = theSession.Parts.Work;
                            Point3d position = new Point3d(-50, 70, 0);
                            foreach (Dimension check_dim in map_dimension)
                            {
                                if (check_dim.GetType().ToString() == "NXOpen.Annotations.PmiParallelDimension")
                                {
                                    Associativity ass1 = check_dim.GetAssociativity(1);
                                    Associativity ass2 = check_dim.GetAssociativity(2);
                                    Edge edge1 = (Edge)ass1.FirstObject;
                                    Edge edge2 = (Edge)ass2.FirstObject;
                                    //我们要找面
                                    Face[] face1 = edge1.GetFaces();
                                    Face[] face2 = edge2.GetFaces();
                                    int loc1 = 100;
                                    int loc2 = 100;
                                    #region 找标注有关的2个面在列表处的索引位置
                                    foreach (Face check_face1 in face1)
                                    {
                                        if (check_face1.SolidFaceType.ToString() == "Planar")
                                        {
                                            int check_index = 0;
                                            foreach (Face compare_face1 in Mapface)
                                            {
                                                if (check_face1 == compare_face1)
                                                {
                                                    loc1 = check_index;
                                                    break;
                                                }
                                                check_index = check_index + 1;
                                            }
                                        }
                                    }
                                    foreach (Face check_face2 in face2)
                                    {
                                        if (check_face2.SolidFaceType.ToString() == "Planar")
                                        {
                                            int check_index = 0;
                                            foreach (Face compare_face2 in Mapface)
                                            {
                                                if (check_face2 == compare_face2)
                                                {
                                                    loc2 = check_index;
                                                    break;
                                                }
                                                check_index = check_index + 1;
                                            }
                                        }
                                    }
                                    #endregion
                                    if ((loc1 == 100) || (loc2 == 100))
                                    {
                                        //即当前尺寸涉及的某个面在映射面中不存在
                                    }
                                    else
                                    {
                                        #region 当前标注的两个面当前工序和映射工序都存在
                                        //如果粗加工时，得到的两个面在映射标注的工序模型里都有
                                        if ((MarkfaceTestpoint.Contains(Mapfacepoint[loc1]) == true) & (MarkfaceTestpoint.Contains(Mapfacepoint[loc2]) == true))
                                        {
                                            int num1;
                                            num1 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc1], 0);
                                            int num2;
                                            num2 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc2], 0);
                                            if ((Markfacepoint.Contains(MarkfaceTestpoint[num1]) == false) & (Markfacepoint.Contains(MarkfaceTestpoint[num2]) == false))
                                            {
                                                //当前标注的两个面，全部都不是制造特征体所得到的
                                            }
                                            else
                                            {
                                                Edge[] mark_edge1 = MarkfaceTest[num1].GetEdges();
                                                Edge[] mark_edge2 = MarkfaceTest[num2].GetEdges();
                                                Dimension markdim;
                                                bool check;
                                                NXFun.CheckOverDimension(MarkfaceTest[num1], MarkfaceTest[num2],Over_check_Face,out check);
                                                if (check == false)
                                                {
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                    Over_check_Face.Add(MarkfaceTest[num1]);
                                                    Over_check_Face.Add(MarkfaceTest[num2]);
                                                }
                                            }
                                        }
                                        #endregion
                                        #region 当前标注有一个面是映射工序里存在着的
                                        //如果有一个面是当前工序得到的，而映射尺寸的工序模型里也有
                                        else if (MarkfaceTestpoint.Contains(Mapfacepoint[loc1]) == true)//只包含一个
                                        {
                                            double[] counts = MarkfaceTestpoint.ToArray();
                                            int count = counts.Length;
                                            int num1;
                                            num1 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc1], 0);
                                            int num2;
                                            num2 = loc2;
                                            if ((num2 < count) & (num1 != num2))
                                            {
                                                bool check;
                                                Edge[] mark_edge1 = MarkfaceTest[num1].GetEdges();
                                                Edge[] mark_edge2 = MarkfaceTest[num2].GetEdges();
                                                NXFun.CheckOverDimension(MarkfaceTest[num1], MarkfaceTest[num2], Over_check_Face, out check);
                                                if (check == false)
                                                {
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                    Over_check_Face.Add(MarkfaceTest[num1]);
                                                    Over_check_Face.Add(MarkfaceTest[num2]);
                                                }
                                            }
                                        }
                                        else if (MarkfaceTestpoint.Contains(Mapfacepoint[loc2]) == true)
                                        {
                                            double[] counts = MarkfaceTestpoint.ToArray();
                                            int count = counts.Length;
                                            int num2;
                                            num2 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc2], 0);
                                            int num1;
                                            num1 = loc1;
                                            if ((num1 < count) & (num1 != num2))
                                            {
                                                bool check;
                                                Edge[] mark_edge1 = MarkfaceTest[num1].GetEdges();
                                                Edge[] mark_edge2 = MarkfaceTest[num2].GetEdges();
                                                NXFun.CheckOverDimension(MarkfaceTest[num1], MarkfaceTest[num2], Over_check_Face, out check);
                                                if (check == false)
                                                {
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                    Over_check_Face.Add(MarkfaceTest[num1]);
                                                    Over_check_Face.Add(MarkfaceTest[num2]);
                                                }
                                            }
                                        }
                                        #endregion
                                        #region 两个面均不是当前工序和映射工序的公共面,那么就按照顺序来
                                        else if ((MarkfaceTestpoint.Contains(Mapfacepoint[loc1]) == false) & (MarkfaceTestpoint.Contains(Mapfacepoint[loc2]) == false))
                                        {
                                            double[] counts = Markfacepoint.ToArray();
                                            int count = counts.Length;
                                            if ((loc1 < count) & (loc2 < count) & (loc1 != loc2))
                                            {
                                                bool check;
                                                Edge[] mark_edge1 = Markface[loc1].GetEdges();
                                                Edge[] mark_edge2 = Markface[loc2].GetEdges();
                                                NXFun.CheckOverDimension(MarkfaceTest[loc1], MarkfaceTest[loc2], Over_check_Face, out check);
                                                if (check == false)
                                                {
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                    Over_check_Face.Add(MarkfaceTest[loc1]);
                                                    Over_check_Face.Add(MarkfaceTest[loc2]);
                                                }
                                            }
                                        }
                                        #endregion
                                    }
                                }
                            }
                            #endregion
                        }
                        //从左边开始
                        else if (start_name.Contains("left") == true)
                        {
                            Markface.Reverse();
                            Markfacepoint.Reverse();
                            Mapface.Reverse();
                            Mapfacepoint.Reverse();
                            MarkfaceTestpoint.Reverse();
                            MarkfaceTest.Reverse();
                            #region 开始找尺寸,这是对应从左边，即从小到大
                            theUFSession.Assem.SetWorkPart(map_part.Tag);
                            Part workPartMark = theSession.Parts.Work;
                            Dimension[] map_dimension = workPartMark.Dimensions.ToArray();//所有尺寸
                            Face[] mark_face_in_target = Markface.ToArray();
                            List<Face> Over_check_Face = new List<Face>();
                            int mark_face_num = mark_face_in_target.Length;
                            theUFSession.Assem.SetWorkPart(start_part.Tag);
                            workPartMark = theSession.Parts.Work;
                            foreach (Dimension check_dim in map_dimension)
                            {
                                if (check_dim.GetType().ToString() == "NXOpen.Annotations.PmiParallelDimension")
                                {
                                    Associativity ass1 = check_dim.GetAssociativity(1);
                                    Associativity ass2 = check_dim.GetAssociativity(2);
                                    Edge edge1 = (Edge)ass1.FirstObject;
                                    Edge edge2 = (Edge)ass2.FirstObject;
                                    //我们要找面
                                    Face[] face1 = edge1.GetFaces();
                                    Face[] face2 = edge2.GetFaces();
                                    int loc1 = 100;
                                    int loc2 = 100;
                                    #region 找标注有关的2个面在列表处的索引位置
                                    foreach (Face check_face1 in face1)
                                    {
                                        if (check_face1.SolidFaceType.ToString() == "Planar")
                                        {
                                            int check_index = 0;
                                            foreach (Face compare_face1 in Mapface)
                                            {
                                                if (check_face1 == compare_face1)
                                                {
                                                    loc1 = check_index;
                                                    break;
                                                }
                                                check_index = check_index + 1;
                                            }
                                        }
                                    }
                                    foreach (Face check_face2 in face2)
                                    {
                                        if (check_face2.SolidFaceType.ToString() == "Planar")
                                        {
                                            int check_index = 0;
                                            foreach (Face compare_face2 in Mapface)
                                            {
                                                if (check_face2 == compare_face2)
                                                {
                                                    loc2 = check_index;
                                                    break;
                                                }
                                                check_index = check_index + 1;
                                            }
                                        }
                                    }
                                    #endregion
                                    if ((loc1 == 100) || (loc2 == 100))
                                    {
                                        //即当前尺寸涉及的某个面在映射面中不存在
                                    }
                                    else
                                    {
                                        #region 当前标注的两个面当前工序和映射工序都存在
                                        //如果粗加工时，得到的两个面在映射标注的工序模型里都有
                                        if ((MarkfaceTestpoint.Contains(Mapfacepoint[loc1]) == true) & (MarkfaceTestpoint.Contains(Mapfacepoint[loc2]) == true))
                                        {
                                            int num1;
                                            num1 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc1], 0);
                                            int num2;
                                            num2 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc2], 0);
                                            if ((Markfacepoint.Contains(MarkfaceTestpoint[num1]) == false) & (Markfacepoint.Contains(MarkfaceTestpoint[num2]) == false))
                                            {
                                                //当前标注的两个面，全部都不是制造特征体所得到的
                                            }
                                            else
                                            {
                                                Edge[] mark_edge1 = MarkfaceTest[num1].GetEdges();
                                                Edge[] mark_edge2 = MarkfaceTest[num2].GetEdges();
                                                Dimension markdim;
                                                bool check;
                                                NXFun.CheckOverDimension(MarkfaceTest[num1], MarkfaceTest[num2], Over_check_Face, out check);
                                                if (check == false)
                                                {
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                    Over_check_Face.Add(MarkfaceTest[num1]);
                                                    Over_check_Face.Add(MarkfaceTest[num2]);
                                                }
                                            }
                                        }
                                        #endregion
                                        #region 当前标注有一个面是映射工序里存在着的
                                        //如果有一个面是当前工序得到的，而映射尺寸的工序模型里也有
                                        else if (MarkfaceTestpoint.Contains(Mapfacepoint[loc1]) == true)//只包含一个
                                        {
                                            double[] counts = MarkfaceTestpoint.ToArray();
                                            int count = counts.Length;
                                            int num1;
                                            num1 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc1], 0);
                                            int num2;
                                            num2 = loc2;
                                            if ((num2 < count) & (num1 != num2))
                                            {
                                                bool check;
                                                Edge[] mark_edge1 = MarkfaceTest[num1].GetEdges();
                                                Edge[] mark_edge2 = MarkfaceTest[num2].GetEdges();
                                                NXFun.CheckOverDimension(MarkfaceTest[num1], MarkfaceTest[num2], Over_check_Face, out check);
                                                if (check == false)
                                                {
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                    Over_check_Face.Add(MarkfaceTest[num1]);
                                                    Over_check_Face.Add(MarkfaceTest[num2]);
                                                }
                                            }
                                        }
                                        else if (MarkfaceTestpoint.Contains(Mapfacepoint[loc2]) == true)
                                        {
                                            double[] counts = MarkfaceTestpoint.ToArray();
                                            int count = counts.Length;
                                            int num2;
                                            num2 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc2], 0);
                                            int num1;
                                            num1 = loc1;
                                            if ((num1 < count) & (num1 != num2))
                                            {
                                                bool check;
                                                Edge[] mark_edge1 = MarkfaceTest[num1].GetEdges();
                                                Edge[] mark_edge2 = MarkfaceTest[num2].GetEdges();
                                                NXFun.CheckOverDimension(MarkfaceTest[num1], MarkfaceTest[num2], Over_check_Face, out check);
                                                if (check == false)
                                                {
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                    Over_check_Face.Add(MarkfaceTest[num1]);
                                                    Over_check_Face.Add(MarkfaceTest[num2]);
                                                }
                                            }
                                        }
                                        #endregion
                                        #region 两个面均不是当前工序和映射工序的公共面,那么就按照顺序来
                                        else if ((MarkfaceTestpoint.Contains(Mapfacepoint[loc1]) == false) & (MarkfaceTestpoint.Contains(Mapfacepoint[loc2]) == false))
                                        {
                                            double[] counts = Markfacepoint.ToArray();
                                            int count = counts.Length;
                                            if ((loc1 < count) & (loc2 < count) & (loc1 != loc2))
                                            {
                                                bool check;
                                                Edge[] mark_edge1 = Markface[loc1].GetEdges();
                                                Edge[] mark_edge2 = Markface[loc2].GetEdges();
                                                NXFun.CheckOverDimension(MarkfaceTest[loc1], MarkfaceTest[loc2], Over_check_Face, out check);
                                                if (check == false)
                                                {
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                    Over_check_Face.Add(MarkfaceTest[loc1]);
                                                    Over_check_Face.Add(MarkfaceTest[loc2]);
                                                }
                                            }
                                        }
                                        #endregion
                                    }
                                }
                            }
                            #endregion
                        }
                        //还未试验成功
                        //既不是加工右边，也不是加工左边，而是对整体进行加工
                        else if ((start_name.Contains("right") == false) & (start_name.Contains("left")) == false)
                        {
                            #region 暂定与加工右边一样，用的是按X轴上的位置从大到小排列
                            theUFSession.Assem.SetWorkPart(map_part.Tag);
                            Part workPartMark = theSession.Parts.Work;
                            Dimension[] map_dimension = workPartMark.Dimensions.ToArray();//所有尺寸
                            Face[] mark_face_in_target = Markface.ToArray();
                            List<Face> Over_check_Face = new List<Face>();
                            int mark_face_num = mark_face_in_target.Length;
                            theUFSession.Assem.SetWorkPart(start_part.Tag);
                            workPartMark = theSession.Parts.Work;
                            Point3d position = new Point3d(-50, 70, 0);
                            foreach (Dimension check_dim in map_dimension)
                            {
                                if (check_dim.GetType().ToString() == "NXOpen.Annotations.PmiParallelDimension")
                                {
                                    Associativity ass1 = check_dim.GetAssociativity(1);
                                    Associativity ass2 = check_dim.GetAssociativity(2);
                                    Edge edge1 = (Edge)ass1.FirstObject;
                                    Edge edge2 = (Edge)ass2.FirstObject;
                                    //我们要找面
                                    Face[] face1 = edge1.GetFaces();
                                    Face[] face2 = edge2.GetFaces();
                                    int loc1 = 100;
                                    int loc2 = 100;
                                    #region 找标注有关的2个面在列表处的索引位置
                                    foreach (Face check_face1 in face1)
                                    {
                                        if (check_face1.SolidFaceType.ToString() == "Planar")
                                        {
                                            int check_index = 0;
                                            foreach (Face compare_face1 in Mapface)
                                            {
                                                if (check_face1 == compare_face1)
                                                {
                                                    loc1 = check_index;
                                                    break;
                                                }
                                                check_index = check_index + 1;
                                            }
                                        }
                                    }
                                    foreach (Face check_face2 in face2)
                                    {
                                        if (check_face2.SolidFaceType.ToString() == "Planar")
                                        {
                                            int check_index = 0;
                                            foreach (Face compare_face2 in Mapface)
                                            {
                                                if (check_face2 == compare_face2)
                                                {
                                                    loc2 = check_index;
                                                    break;
                                                }
                                                check_index = check_index + 1;
                                            }
                                        }
                                    }
                                    #endregion
                                    if ((loc1 == 100) || (loc2 == 100))
                                    {
                                        //即当前尺寸涉及的某个面在映射面中不存在
                                    }
                                    else
                                    {
                                        #region 当前标注的两个面当前工序和映射工序都存在
                                        //如果粗加工时，得到的两个面在映射标注的工序模型里都有
                                        if ((MarkfaceTestpoint.Contains(Mapfacepoint[loc1]) == true) & (MarkfaceTestpoint.Contains(Mapfacepoint[loc2]) == true))
                                        {
                                            int num1;
                                            num1 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc1], 0);
                                            int num2;
                                            num2 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc2], 0);
                                            if ((Markfacepoint.Contains(MarkfaceTestpoint[num1]) == false) & (Markfacepoint.Contains(MarkfaceTestpoint[num2]) == false))
                                            {
                                                //当前标注的两个面，全部都不是制造特征体所得到的
                                            }
                                            else
                                            {
                                                Edge[] mark_edge1 = MarkfaceTest[num1].GetEdges();
                                                Edge[] mark_edge2 = MarkfaceTest[num2].GetEdges();
                                                Dimension markdim;
                                                bool check;
                                                NXFun.CheckOverDimension(MarkfaceTest[num1], MarkfaceTest[num2], Over_check_Face, out check);
                                                if (check == false)
                                                {
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                    Over_check_Face.Add(MarkfaceTest[num1]);
                                                    Over_check_Face.Add(MarkfaceTest[num2]);
                                                }
                                            }
                                        }
                                        #endregion
                                        #region 当前标注有一个面是映射工序里存在着的
                                        //如果有一个面是当前工序得到的，而映射尺寸的工序模型里也有
                                        else if (MarkfaceTestpoint.Contains(Mapfacepoint[loc1]) == true)//只包含一个
                                        {
                                            double[] counts = MarkfaceTestpoint.ToArray();
                                            int count = counts.Length;
                                            int num1;
                                            num1 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc1], 0);
                                            int num2;
                                            num2 = loc2;
                                            if ((num2 < count) & (num1 != num2))
                                            {
                                                bool check;
                                                Edge[] mark_edge1 = MarkfaceTest[num1].GetEdges();
                                                Edge[] mark_edge2 = MarkfaceTest[num2].GetEdges();
                                                NXFun.CheckOverDimension(MarkfaceTest[num1], MarkfaceTest[num2], Over_check_Face, out check);
                                                if (check == false)
                                                {
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                    Over_check_Face.Add(MarkfaceTest[num1]);
                                                    Over_check_Face.Add(MarkfaceTest[num2]);
                                                }
                                            }
                                        }
                                        else if (MarkfaceTestpoint.Contains(Mapfacepoint[loc2]) == true)
                                        {
                                            double[] counts = MarkfaceTestpoint.ToArray();
                                            int count = counts.Length;
                                            int num2;
                                            num2 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc2], 0);
                                            int num1;
                                            num1 = loc1;
                                            if ((num1 < count) & (num1 != num2))
                                            {
                                                bool check;
                                                Edge[] mark_edge1 = MarkfaceTest[num1].GetEdges();
                                                Edge[] mark_edge2 = MarkfaceTest[num2].GetEdges();
                                                NXFun.CheckOverDimension(MarkfaceTest[num1], MarkfaceTest[num2], Over_check_Face, out check);
                                                if (check == false)
                                                {
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                    Over_check_Face.Add(MarkfaceTest[num1]);
                                                    Over_check_Face.Add(MarkfaceTest[num2]);
                                                }
                                            }
                                        }
                                        #endregion
                                        #region 两个面均不是当前工序和映射工序的公共面,那么就按照顺序来
                                        else if ((MarkfaceTestpoint.Contains(Mapfacepoint[loc1]) == false) & (MarkfaceTestpoint.Contains(Mapfacepoint[loc2]) == false))
                                        {
                                            double[] counts = Markfacepoint.ToArray();
                                            int count = counts.Length;
                                            if ((loc1 < count) & (loc2 < count) & (loc1 != loc2))
                                            {
                                                bool check;
                                                Edge[] mark_edge1 = Markface[loc1].GetEdges();
                                                Edge[] mark_edge2 = Markface[loc2].GetEdges();
                                                NXFun.CheckOverDimension(MarkfaceTest[loc1], MarkfaceTest[loc2], Over_check_Face, out check);
                                                if (check == false)
                                                {
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                    Over_check_Face.Add(MarkfaceTest[loc1]);
                                                    Over_check_Face.Add(MarkfaceTest[loc2]);
                                                }
                                            }
                                        }
                                        #endregion
                                    }
                                }
                            }
                            #endregion
                        }
                        //未试验成功
                        #endregion
                    }
                    #endregion

                    #region 粗加工
                    else if (start_name.Contains("rough") == true)
                    {
                        #region 加工左端
                        if (start_name.Contains("left") == true)
                        {
                            for (int j = i - 1; j >= 0; j--)
                            {
                                if (get_name[j].Contains("left") == true)
                                {
                                    map_part = (Part)theSession.Parts.FindObject(get_name[j]);
                                    theUFSession.Assem.SetWorkPart(map_part.Tag);
                                    Part workPart = theSession.Parts.Work;
                                    Body[] mapbody = workPart.Bodies.ToArray();
                                    Body map_body = mapbody[0];
                                    Face[] map_face = map_body.GetFaces();
                                    NXFun.GetFaceWithPoint_Rough(map_face, out Mapface, out Mapfacepoint);
                                    break;
                                }
                            }
                            #region 添加公共面
                            //如果当前工序模型和映射模型有相同的面，那么我们也要加到标注面里头去，但不能全加，因此要利用那个loc1和loc2
                            //对loc1和loc2对应的平面进行筛选，如果2个都不在原来的里头，那么就不标
                            List<Face> MarkfaceTest = new List<Face>();
                            List<double> MarkfaceTestpoint = new List<double>();
                            theUFSession.Assem.SetWorkPart(start_part.Tag);
                            Part TestPart = theSession.Parts.Work;
                            Body[] Testbodies = TestPart.Bodies.ToArray();
                            Body testbody = Testbodies[0];
                            Face[] test_face = testbody.GetFaces();
                            //直接继承之前的，我们可以发现肯定不会为0，所以就不用考虑加上temp了。
                            MarkfaceTest.AddRange(Markface);//用来对比
                            MarkfaceTestpoint.AddRange(Markfacepoint);//用来对比
                            foreach (Face testface in test_face)
                            {
                                double[] point_num_get = MarkfaceTestpoint.ToArray();
                                int num = point_num_get.Length;//得到点的个数，从而应对如果现在判断的点是最小的
                                if (num == 0)
                                {
                                    break;
                                }
                                int type;
                                double[] point = new double[6];
                                double[] dir = new double[5];
                                double[] box = new double[6];
                                double radius;
                                double rad_data;
                                int norm_dir;
                                theUFSession.Modl.AskFaceData(testface.Tag, out type, point, dir, box, out radius, out rad_data, out norm_dir);
                                if (type == 22)
                                {
                                    if (Mapfacepoint.Contains(point[0]) == true)//即当前所选择的面在映射尺寸那儿也有
                                    {
                                        int index = 0;
                                        foreach (double check_point in MarkfaceTestpoint)
                                        {
                                            if ((check_point < point[0]) & (MarkfaceTestpoint.Contains(point[0]) == false))
                                            {
                                                MarkfaceTestpoint.Insert(index, point[0]);
                                                MarkfaceTest.Insert(index, testface);
                                                break;
                                            }
                                            index = index + 1;
                                            if ((index == num) & (MarkfaceTestpoint.Contains(point[0]) == false))
                                            {
                                                MarkfaceTestpoint.Add(point[0]);
                                                MarkfaceTest.Add(testface);
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                            #endregion

                            #region 开始映射
                            //如果是从右边开始
                            if (start_name.Contains("right") == true)
                            {
                                #region 开始找尺寸，这是对应从大到小，从右边开始
                                theUFSession.Assem.SetWorkPart(map_part.Tag);
                                Part workPartMark = theSession.Parts.Work;
                                Dimension[] map_dimension = workPartMark.Dimensions.ToArray();//所有尺寸
                                Face[] mark_face_in_target = Markface.ToArray();
                                List<Face> Over_check_Face = new List<Face>();
                                int mark_face_num = mark_face_in_target.Length;
                                theUFSession.Assem.SetWorkPart(start_part.Tag);
                                workPartMark = theSession.Parts.Work;
                                Point3d position = new Point3d(-50, 70, 0);
                                foreach (Dimension check_dim in map_dimension)
                                {
                                    if (check_dim.GetType().ToString() == "NXOpen.Annotations.PmiParallelDimension")
                                    {
                                        Associativity ass1 = check_dim.GetAssociativity(1);
                                        Associativity ass2 = check_dim.GetAssociativity(2);
                                        Edge edge1 = (Edge)ass1.FirstObject;
                                        Edge edge2 = (Edge)ass2.FirstObject;
                                        //我们要找面
                                        Face[] face1 = edge1.GetFaces();
                                        Face[] face2 = edge2.GetFaces();
                                        int loc1 = 100;
                                        int loc2 = 100;
                                        #region 找标注有关的2个面在列表处的索引位置
                                        foreach (Face check_face1 in face1)
                                        {
                                            if (check_face1.SolidFaceType.ToString() == "Planar")
                                            {
                                                int check_index = 0;
                                                foreach (Face compare_face1 in Mapface)
                                                {
                                                    if (check_face1 == compare_face1)
                                                    {
                                                        loc1 = check_index;
                                                        break;
                                                    }
                                                    check_index = check_index + 1;
                                                }
                                            }
                                        }
                                        foreach (Face check_face2 in face2)
                                        {
                                            if (check_face2.SolidFaceType.ToString() == "Planar")
                                            {
                                                int check_index = 0;
                                                foreach (Face compare_face2 in Mapface)
                                                {
                                                    if (check_face2 == compare_face2)
                                                    {
                                                        loc2 = check_index;
                                                        break;
                                                    }
                                                    check_index = check_index + 1;
                                                }
                                            }
                                        }
                                        #endregion
                                        if ((loc1 == 100) || (loc2 == 100))
                                        {
                                            //即当前尺寸涉及的某个面在映射面中不存在
                                        }
                                        else
                                        {
                                            #region 当前标注的两个面当前工序和映射工序都存在
                                            //如果粗加工时，得到的两个面在映射标注的工序模型里都有
                                            if ((MarkfaceTestpoint.Contains(Mapfacepoint[loc1]) == true) & (MarkfaceTestpoint.Contains(Mapfacepoint[loc2]) == true))
                                            {
                                                int num1;
                                                num1 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc1], 0);
                                                int num2;
                                                num2 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc2], 0);
                                                if ((Markfacepoint.Contains(MarkfaceTestpoint[num1]) == false) & (Markfacepoint.Contains(MarkfaceTestpoint[num2]) == false))
                                                {
                                                    //当前标注的两个面，全部都不是制造特征体所得到的
                                                }
                                                else
                                                {
                                                    Edge[] mark_edge1 = MarkfaceTest[num1].GetEdges();
                                                    Edge[] mark_edge2 = MarkfaceTest[num2].GetEdges();
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                }
                                            }
                                            #endregion
                                            #region 当前标注有一个面是映射工序里存在着的
                                            //如果有一个面是当前工序得到的，而映射尺寸的工序模型里也有
                                            else if (MarkfaceTestpoint.Contains(Mapfacepoint[loc1]) == true)//只包含一个
                                            {
                                                double[] counts = MarkfaceTestpoint.ToArray();
                                                int count = counts.Length;
                                                int num1;
                                                num1 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc1], 0);
                                                int num2;
                                                num2 = loc2;
                                                if ((num2 < count) & (num1 != num2))
                                                {
                                                    Edge[] mark_edge1 = MarkfaceTest[num1].GetEdges();
                                                    Edge[] mark_edge2 = MarkfaceTest[num2].GetEdges();
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                }
                                            }
                                            else if (MarkfaceTestpoint.Contains(Mapfacepoint[loc2]) == true)
                                            {
                                                double[] counts = MarkfaceTestpoint.ToArray();
                                                int count = counts.Length;
                                                int num2;
                                                num2 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc2], 0);
                                                int num1;
                                                num1 = loc1;
                                                if ((num1 < count) & (num1 != num2))
                                                {
                                                    Edge[] mark_edge1 = MarkfaceTest[num1].GetEdges();
                                                    Edge[] mark_edge2 = MarkfaceTest[num2].GetEdges();
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                }
                                            }
                                            #endregion
                                            #region 两个面均不是当前工序和映射工序的公共面,那么就按照顺序来
                                            else if ((MarkfaceTestpoint.Contains(Mapfacepoint[loc1]) == false) & (MarkfaceTestpoint.Contains(Mapfacepoint[loc2]) == false))
                                            {
                                                double[] counts = Markfacepoint.ToArray();
                                                int count = counts.Length;
                                                if ((loc1 < count) & (loc2 < count) & (loc1 != loc2))
                                                {
                                                    Edge[] mark_edge1 = Markface[loc1].GetEdges();
                                                    Edge[] mark_edge2 = Markface[loc2].GetEdges();
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                }
                                            }
                                            #endregion
                                        }
                                    }
                                }
                                #endregion
                            }
                            //从左边开始
                            else if (start_name.Contains("left") == true)
                            {

                                Markface.Reverse();
                                Markfacepoint.Reverse();
                                Mapface.Reverse();
                                Mapfacepoint.Reverse();
                                MarkfaceTestpoint.Reverse();
                                MarkfaceTest.Reverse();
                                #region 开始找尺寸,这是对应从左边，即从小到大
                                theUFSession.Assem.SetWorkPart(map_part.Tag);
                                Part workPartMark = theSession.Parts.Work;
                                List<Face> Over_check_Face = new List<Face>();
                                Dimension[] map_dimension = workPartMark.Dimensions.ToArray();//所有尺寸
                                Face[] mark_face_in_target = Markface.ToArray();
                                int mark_face_num = mark_face_in_target.Length;
                                theUFSession.Assem.SetWorkPart(start_part.Tag);
                                workPartMark = theSession.Parts.Work;
                                foreach (Dimension check_dim in map_dimension)
                                {
                                    if (check_dim.GetType().ToString() == "NXOpen.Annotations.PmiParallelDimension")
                                    {
                                        Associativity ass1 = check_dim.GetAssociativity(1);
                                        Associativity ass2 = check_dim.GetAssociativity(2);
                                        Edge edge1 = (Edge)ass1.FirstObject;
                                        Edge edge2 = (Edge)ass2.FirstObject;
                                        //我们要找面
                                        Face[] face1 = edge1.GetFaces();
                                        Face[] face2 = edge2.GetFaces();
                                        int loc1 = 100;
                                        int loc2 = 100;
                                        #region 找标注有关的2个面在列表处的索引位置
                                        foreach (Face check_face1 in face1)
                                        {
                                            if (check_face1.SolidFaceType.ToString() == "Planar")
                                            {
                                                int check_index = 0;
                                                foreach (Face compare_face1 in Mapface)
                                                {
                                                    if (check_face1 == compare_face1)
                                                    {
                                                        loc1 = check_index;
                                                        break;
                                                    }
                                                    check_index = check_index + 1;
                                                }
                                            }
                                        }
                                        foreach (Face check_face2 in face2)
                                        {
                                            if (check_face2.SolidFaceType.ToString() == "Planar")
                                            {
                                                int check_index = 0;
                                                foreach (Face compare_face2 in Mapface)
                                                {
                                                    if (check_face2 == compare_face2)
                                                    {
                                                        loc2 = check_index;
                                                        break;
                                                    }
                                                    check_index = check_index + 1;
                                                }
                                            }
                                        }
                                        #endregion
                                        if ((loc1 == 100) || (loc2 == 100))
                                        {
                                            //即当前尺寸涉及的某个面在映射面中不存在
                                        }
                                        else
                                        {
                                            #region 当前标注的两个面当前工序和映射工序都存在
                                            //如果粗加工时，得到的两个面在映射标注的工序模型里都有
                                            if ((MarkfaceTestpoint.Contains(Mapfacepoint[loc1]) == true) & (MarkfaceTestpoint.Contains(Mapfacepoint[loc2]) == true))
                                            {
                                                int num1;
                                                num1 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc1], 0);
                                                int num2;
                                                num2 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc2], 0);
                                                if ((Markfacepoint.Contains(MarkfaceTestpoint[num1]) == false) & (Markfacepoint.Contains(MarkfaceTestpoint[num2]) == false))
                                                {
                                                    //当前标注的两个面，全部都不是制造特征体所得到的
                                                }
                                                else
                                                {
                                                    Edge[] mark_edge1 = MarkfaceTest[num1].GetEdges();
                                                    Edge[] mark_edge2 = MarkfaceTest[num2].GetEdges();
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                }
                                            }
                                            #endregion
                                            #region 当前标注有一个面是映射工序里存在着的
                                            //如果有一个面是当前工序得到的，而映射尺寸的工序模型里也有
                                            else if (MarkfaceTestpoint.Contains(Mapfacepoint[loc1]) == true)//只包含一个
                                            {
                                                double[] counts = MarkfaceTestpoint.ToArray();
                                                int count = counts.Length;
                                                int num1;
                                                num1 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc1], 0);
                                                int num2;
                                                num2 = loc2;
                                                if ((num2 < count) & (num1 != num2))
                                                {
                                                    Edge[] mark_edge1 = MarkfaceTest[num1].GetEdges();
                                                    Edge[] mark_edge2 = MarkfaceTest[num2].GetEdges();
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                }
                                            }
                                            else if (MarkfaceTestpoint.Contains(Mapfacepoint[loc2]) == true)
                                            {
                                                double[] counts = MarkfaceTestpoint.ToArray();
                                                int count = counts.Length;
                                                int num2;
                                                num2 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc2], 0);
                                                int num1;
                                                num1 = loc1;
                                                if ((num1 < count) & (num1 != num2))
                                                {
                                                    Edge[] mark_edge1 = MarkfaceTest[num1].GetEdges();
                                                    Edge[] mark_edge2 = MarkfaceTest[num2].GetEdges();
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                }
                                            }
                                            #endregion
                                            #region 两个面均不是当前工序和映射工序的公共面,那么就按照顺序来
                                            else if ((MarkfaceTestpoint.Contains(Mapfacepoint[loc1]) == false) & (MarkfaceTestpoint.Contains(Mapfacepoint[loc2]) == false))
                                            {
                                                double[] counts = Markfacepoint.ToArray();
                                                int count = counts.Length;
                                                if ((loc1 < count) & (loc2 < count) & (loc1 != loc2))
                                                {
                                                    Edge[] mark_edge1 = Markface[loc1].GetEdges();
                                                    Edge[] mark_edge2 = Markface[loc2].GetEdges();
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                }
                                            }
                                            #endregion
                                        }
                                    }
                                }
                                #endregion
                            }
                            //还未试验成功
                            //既不是加工右边，也不是加工左边，而是对整体进行加工
                            else if ((start_name.Contains("right") == false) & (start_name.Contains("left")) == false)
                            {
                                #region 暂定与加工右边一样，用的是按X轴上的位置从大到小排列
                                theUFSession.Assem.SetWorkPart(map_part.Tag);
                                Part workPartMark = theSession.Parts.Work;
                                Dimension[] map_dimension = workPartMark.Dimensions.ToArray();//所有尺寸
                                Face[] mark_face_in_target = Markface.ToArray();
                                List<Face> Over_check_Face = new List<Face>();
                                int mark_face_num = mark_face_in_target.Length;
                                theUFSession.Assem.SetWorkPart(start_part.Tag);
                                workPartMark = theSession.Parts.Work;
                                Point3d position = new Point3d(-50, 70, 0);
                                foreach (Dimension check_dim in map_dimension)
                                {
                                    if (check_dim.GetType().ToString() == "NXOpen.Annotations.PmiParallelDimension")
                                    {
                                        Associativity ass1 = check_dim.GetAssociativity(1);
                                        Associativity ass2 = check_dim.GetAssociativity(2);
                                        Edge edge1 = (Edge)ass1.FirstObject;
                                        Edge edge2 = (Edge)ass2.FirstObject;
                                        //我们要找面
                                        Face[] face1 = edge1.GetFaces();
                                        Face[] face2 = edge2.GetFaces();
                                        int loc1 = 100;
                                        int loc2 = 100;
                                        #region 找标注有关的2个面在列表处的索引位置
                                        foreach (Face check_face1 in face1)
                                        {
                                            if (check_face1.SolidFaceType.ToString() == "Planar")
                                            {
                                                int check_index = 0;
                                                foreach (Face compare_face1 in Mapface)
                                                {
                                                    if (check_face1 == compare_face1)
                                                    {
                                                        loc1 = check_index;
                                                        break;
                                                    }
                                                    check_index = check_index + 1;
                                                }
                                            }
                                        }
                                        foreach (Face check_face2 in face2)
                                        {
                                            if (check_face2.SolidFaceType.ToString() == "Planar")
                                            {
                                                int check_index = 0;
                                                foreach (Face compare_face2 in Mapface)
                                                {
                                                    if (check_face2 == compare_face2)
                                                    {
                                                        loc2 = check_index;
                                                        break;
                                                    }
                                                    check_index = check_index + 1;
                                                }
                                            }
                                        }
                                        #endregion
                                        if ((loc1 == 100) || (loc2 == 100))
                                        {
                                            //即当前尺寸涉及的某个面在映射面中不存在
                                        }
                                        else
                                        {
                                            #region 当前标注的两个面当前工序和映射工序都存在
                                            //如果粗加工时，得到的两个面在映射标注的工序模型里都有
                                            if ((MarkfaceTestpoint.Contains(Mapfacepoint[loc1]) == true) & (MarkfaceTestpoint.Contains(Mapfacepoint[loc2]) == true))
                                            {
                                                int num1;
                                                num1 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc1], 0);
                                                int num2;
                                                num2 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc2], 0);
                                                if ((Markfacepoint.Contains(MarkfaceTestpoint[num1]) == false) & (Markfacepoint.Contains(MarkfaceTestpoint[num2]) == false))
                                                {
                                                    //当前标注的两个面，全部都不是制造特征体所得到的
                                                }
                                                else
                                                {
                                                    Edge[] mark_edge1 = MarkfaceTest[num1].GetEdges();
                                                    Edge[] mark_edge2 = MarkfaceTest[num2].GetEdges();
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                }
                                            }
                                            #endregion
                                            #region 当前标注有一个面是映射工序里存在着的
                                            //如果有一个面是当前工序得到的，而映射尺寸的工序模型里也有
                                            else if (MarkfaceTestpoint.Contains(Mapfacepoint[loc1]) == true)//只包含一个
                                            {
                                                double[] counts = MarkfaceTestpoint.ToArray();
                                                int count = counts.Length;
                                                int num1;
                                                num1 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc1], 0);
                                                int num2;
                                                num2 = loc2;
                                                if ((num2 < count) & (num1 != num2))
                                                {
                                                    Edge[] mark_edge1 = MarkfaceTest[num1].GetEdges();
                                                    Edge[] mark_edge2 = MarkfaceTest[num2].GetEdges();
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                }
                                            }
                                            else if (MarkfaceTestpoint.Contains(Mapfacepoint[loc2]) == true)
                                            {
                                                double[] counts = MarkfaceTestpoint.ToArray();
                                                int count = counts.Length;
                                                int num2;
                                                num2 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc2], 0);
                                                int num1;
                                                num1 = loc1;
                                                if ((num1 < count) & (num1 != num2))
                                                {
                                                    Edge[] mark_edge1 = MarkfaceTest[num1].GetEdges();
                                                    Edge[] mark_edge2 = MarkfaceTest[num2].GetEdges();
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                }
                                            }
                                            #endregion
                                            #region 两个面均不是当前工序和映射工序的公共面,那么就按照顺序来
                                            else if ((MarkfaceTestpoint.Contains(Mapfacepoint[loc1]) == false) & (MarkfaceTestpoint.Contains(Mapfacepoint[loc2]) == false))
                                            {
                                                double[] counts = Markfacepoint.ToArray();
                                                int count = counts.Length;
                                                if ((loc1 < count) & (loc2 < count) & (loc1 != loc2))
                                                {
                                                    Edge[] mark_edge1 = Markface[loc1].GetEdges();
                                                    Edge[] mark_edge2 = Markface[loc2].GetEdges();
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                }
                                            }
                                            #endregion
                                        }
                                    }
                                }
                                #endregion
                            }
                            //未试验成功
                            #endregion
                        }
                        #endregion
                        #region 加工右端
                        else if (start_name.Contains("right") == true)
                        {
                            for (int j = i - 1; j >= 0; j--)
                            {
                                if (get_name[j].Contains("right") == true)
                                {
                                    map_part = (Part)theSession.Parts.FindObject(get_name[j]);
                                    theUFSession.Assem.SetWorkPart(map_part.Tag);
                                    Part workPart = theSession.Parts.Work;
                                    Body[] mapbody = workPart.Bodies.ToArray();
                                    Body map_body = mapbody[0];
                                    Face[] map_face = map_body.GetFaces();
                                    NXFun.GetFaceWithPoint_Rough(map_face, out Mapface, out Mapfacepoint);
                                    break;
                                }
                            }
                            #region 添加公共面
                            //如果当前工序模型和映射模型有相同的面，那么我们也要加到标注面里头去，但不能全加，因此要利用那个loc1和loc2
                            //对loc1和loc2对应的平面进行筛选，如果2个都不在原来的里头，那么就不标
                            List<Face> MarkfaceTest = new List<Face>();
                            List<double> MarkfaceTestpoint = new List<double>();
                            theUFSession.Assem.SetWorkPart(start_part.Tag);
                            Part TestPart = theSession.Parts.Work;
                            Body[] Testbodies = TestPart.Bodies.ToArray();
                            Body testbody = Testbodies[0];
                            Face[] test_face = testbody.GetFaces();
                            //直接继承之前的，我们可以发现肯定不会为0，所以就不用考虑加上temp了。
                            MarkfaceTest.AddRange(Markface);//用来对比
                            MarkfaceTestpoint.AddRange(Markfacepoint);//用来对比
                            foreach (Face testface in test_face)
                            {
                                double[] point_num_get = MarkfaceTestpoint.ToArray();
                                int num = point_num_get.Length;//得到点的个数，从而应对如果现在判断的点是最小的
                                if (num == 0)
                                {
                                    break;
                                }
                                int type;
                                double[] point = new double[6];
                                double[] dir = new double[5];
                                double[] box = new double[6];
                                double radius;
                                double rad_data;
                                int norm_dir;
                                theUFSession.Modl.AskFaceData(testface.Tag, out type, point, dir, box, out radius, out rad_data, out norm_dir);
                                if (type == 22)
                                {
                                    if (Mapfacepoint.Contains(point[0]) == true)//即当前所选择的面在映射尺寸那儿也有
                                    {
                                        int index = 0;
                                        foreach (double check_point in MarkfaceTestpoint)
                                        {
                                            if ((check_point < point[0]) & (MarkfaceTestpoint.Contains(point[0]) == false))
                                            {
                                                MarkfaceTestpoint.Insert(index, point[0]);
                                                MarkfaceTest.Insert(index, testface);
                                                break;
                                            }
                                            index = index + 1;
                                            if ((index == num) & (MarkfaceTestpoint.Contains(point[0]) == false))
                                            {
                                                MarkfaceTestpoint.Add(point[0]);
                                                MarkfaceTest.Add(testface);
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                            #endregion

                            #region 开始映射
                            //如果是从右边开始
                            if (start_name.Contains("right") == true)
                            {
                                #region 开始找尺寸，这是对应从大到小，从右边开始
                                theUFSession.Assem.SetWorkPart(map_part.Tag);
                                Part workPartMark = theSession.Parts.Work;
                                List<Face> Over_check_Face = new List<Face>();
                                Dimension[] map_dimension = workPartMark.Dimensions.ToArray();//所有尺寸
                                Face[] mark_face_in_target = Markface.ToArray();
                                int mark_face_num = mark_face_in_target.Length;
                                theUFSession.Assem.SetWorkPart(start_part.Tag);
                                workPartMark = theSession.Parts.Work;
                                Point3d position = new Point3d(-50, 70, 0);
                                foreach (Dimension check_dim in map_dimension)
                                {
                                    if (check_dim.GetType().ToString() == "NXOpen.Annotations.PmiParallelDimension")
                                    {
                                        Associativity ass1 = check_dim.GetAssociativity(1);
                                        Associativity ass2 = check_dim.GetAssociativity(2);
                                        Edge edge1 = (Edge)ass1.FirstObject;
                                        Edge edge2 = (Edge)ass2.FirstObject;
                                        //我们要找面
                                        Face[] face1 = edge1.GetFaces();
                                        Face[] face2 = edge2.GetFaces();
                                        int loc1 = 100;
                                        int loc2 = 100;
                                        #region 找标注有关的2个面在列表处的索引位置
                                        foreach (Face check_face1 in face1)
                                        {
                                            if (check_face1.SolidFaceType.ToString() == "Planar")
                                            {
                                                int check_index = 0;
                                                foreach (Face compare_face1 in Mapface)
                                                {
                                                    if (check_face1 == compare_face1)
                                                    {
                                                        loc1 = check_index;
                                                        break;
                                                    }
                                                    check_index = check_index + 1;
                                                }
                                            }
                                        }
                                        foreach (Face check_face2 in face2)
                                        {
                                            if (check_face2.SolidFaceType.ToString() == "Planar")
                                            {
                                                int check_index = 0;
                                                foreach (Face compare_face2 in Mapface)
                                                {
                                                    if (check_face2 == compare_face2)
                                                    {
                                                        loc2 = check_index;
                                                        break;
                                                    }
                                                    check_index = check_index + 1;
                                                }
                                            }
                                        }
                                        #endregion
                                        if ((loc1 == 100) || (loc2 == 100))
                                        {
                                            //即当前尺寸涉及的某个面在映射面中不存在
                                        }
                                        else
                                        {
                                            #region 当前标注的两个面当前工序和映射工序都存在
                                            //如果粗加工时，得到的两个面在映射标注的工序模型里都有
                                            if ((MarkfaceTestpoint.Contains(Mapfacepoint[loc1]) == true) & (MarkfaceTestpoint.Contains(Mapfacepoint[loc2]) == true))
                                            {
                                                int num1;
                                                num1 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc1], 0);
                                                int num2;
                                                num2 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc2], 0);
                                                if ((Markfacepoint.Contains(MarkfaceTestpoint[num1]) == false) & (Markfacepoint.Contains(MarkfaceTestpoint[num2]) == false))
                                                {
                                                    //当前标注的两个面，全部都不是制造特征体所得到的
                                                }
                                                else
                                                {
                                                    Edge[] mark_edge1 = MarkfaceTest[num1].GetEdges();
                                                    Edge[] mark_edge2 = MarkfaceTest[num2].GetEdges();
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                }
                                            }
                                            #endregion
                                            #region 当前标注有一个面是映射工序里存在着的
                                            //如果有一个面是当前工序得到的，而映射尺寸的工序模型里也有
                                            else if (MarkfaceTestpoint.Contains(Mapfacepoint[loc1]) == true)//只包含一个
                                            {
                                                double[] counts = MarkfaceTestpoint.ToArray();
                                                int count = counts.Length;
                                                int num1;
                                                num1 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc1], 0);
                                                int num2;
                                                num2 = loc2;
                                                if ((num2 < count) & (num1 != num2))
                                                {
                                                    Edge[] mark_edge1 = MarkfaceTest[num1].GetEdges();
                                                    Edge[] mark_edge2 = MarkfaceTest[num2].GetEdges();
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                }
                                            }
                                            else if (MarkfaceTestpoint.Contains(Mapfacepoint[loc2]) == true)
                                            {
                                                double[] counts = MarkfaceTestpoint.ToArray();
                                                int count = counts.Length;
                                                int num2;
                                                num2 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc2], 0);
                                                int num1;
                                                num1 = loc1;
                                                if ((num1 < count) & (num1 != num2))
                                                {
                                                    Edge[] mark_edge1 = MarkfaceTest[num1].GetEdges();
                                                    Edge[] mark_edge2 = MarkfaceTest[num2].GetEdges();
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                }
                                            }
                                            #endregion
                                            #region 两个面均不是当前工序和映射工序的公共面,那么就按照顺序来
                                            else if ((MarkfaceTestpoint.Contains(Mapfacepoint[loc1]) == false) & (MarkfaceTestpoint.Contains(Mapfacepoint[loc2]) == false))
                                            {
                                                double[] counts = Markfacepoint.ToArray();
                                                int count = counts.Length;
                                                if ((loc1 < count) & (loc2 < count) & (loc1 != loc2))
                                                {
                                                    Edge[] mark_edge1 = Markface[loc1].GetEdges();
                                                    Edge[] mark_edge2 = Markface[loc2].GetEdges();
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                }
                                            }
                                            #endregion
                                        }
                                    }
                                }
                                #endregion
                            }
                            //从左边开始
                            else if (start_name.Contains("left") == true)
                            {
                                Markface.Reverse();
                                Markfacepoint.Reverse();
                                Mapface.Reverse();
                                Mapfacepoint.Reverse();
                                MarkfaceTestpoint.Reverse();
                                MarkfaceTest.Reverse();
                                #region 开始找尺寸,这是对应从左边，即从小到大
                                theUFSession.Assem.SetWorkPart(map_part.Tag);
                                Part workPartMark = theSession.Parts.Work;
                                List<Face> Over_check_Face = new List<Face>();
                                Dimension[] map_dimension = workPartMark.Dimensions.ToArray();//所有尺寸
                                Face[] mark_face_in_target = Markface.ToArray();
                                int mark_face_num = mark_face_in_target.Length;
                                theUFSession.Assem.SetWorkPart(start_part.Tag);
                                workPartMark = theSession.Parts.Work;
                                foreach (Dimension check_dim in map_dimension)
                                {
                                    if (check_dim.GetType().ToString() == "NXOpen.Annotations.PmiParallelDimension")
                                    {
                                        Associativity ass1 = check_dim.GetAssociativity(1);
                                        Associativity ass2 = check_dim.GetAssociativity(2);
                                        Edge edge1 = (Edge)ass1.FirstObject;
                                        Edge edge2 = (Edge)ass2.FirstObject;
                                        //我们要找面
                                        Face[] face1 = edge1.GetFaces();
                                        Face[] face2 = edge2.GetFaces();
                                        int loc1 = 100;
                                        int loc2 = 100;
                                        #region 找标注有关的2个面在列表处的索引位置
                                        foreach (Face check_face1 in face1)
                                        {
                                            if (check_face1.SolidFaceType.ToString() == "Planar")
                                            {
                                                int check_index = 0;
                                                foreach (Face compare_face1 in Mapface)
                                                {
                                                    if (check_face1 == compare_face1)
                                                    {
                                                        loc1 = check_index;
                                                        break;
                                                    }
                                                    check_index = check_index + 1;
                                                }
                                            }
                                        }
                                        foreach (Face check_face2 in face2)
                                        {
                                            if (check_face2.SolidFaceType.ToString() == "Planar")
                                            {
                                                int check_index = 0;
                                                foreach (Face compare_face2 in Mapface)
                                                {
                                                    if (check_face2 == compare_face2)
                                                    {
                                                        loc2 = check_index;
                                                        break;
                                                    }
                                                    check_index = check_index + 1;
                                                }
                                            }
                                        }
                                        #endregion
                                        if ((loc1 == 100) || (loc2 == 100))
                                        {
                                            //即当前尺寸涉及的某个面在映射面中不存在
                                        }
                                        else
                                        {
                                            #region 当前标注的两个面当前工序和映射工序都存在
                                            //如果粗加工时，得到的两个面在映射标注的工序模型里都有
                                            if ((MarkfaceTestpoint.Contains(Mapfacepoint[loc1]) == true) & (MarkfaceTestpoint.Contains(Mapfacepoint[loc2]) == true))
                                            {
                                                int num1;
                                                num1 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc1], 0);
                                                int num2;
                                                num2 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc2], 0);
                                                if ((Markfacepoint.Contains(MarkfaceTestpoint[num1]) == false) & (Markfacepoint.Contains(MarkfaceTestpoint[num2]) == false))
                                                {
                                                    //当前标注的两个面，全部都不是制造特征体所得到的
                                                }
                                                else
                                                {
                                                    Edge[] mark_edge1 = MarkfaceTest[num1].GetEdges();
                                                    Edge[] mark_edge2 = MarkfaceTest[num2].GetEdges();
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                }
                                            }
                                            #endregion
                                            #region 当前标注有一个面是映射工序里存在着的
                                            //如果有一个面是当前工序得到的，而映射尺寸的工序模型里也有
                                            else if (MarkfaceTestpoint.Contains(Mapfacepoint[loc1]) == true)//只包含一个
                                            {
                                                double[] counts = MarkfaceTestpoint.ToArray();
                                                int count = counts.Length;
                                                int num1;
                                                num1 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc1], 0);
                                                int num2;
                                                num2 = loc2;
                                                if ((num2 < count) & (num1 != num2))
                                                {
                                                    Edge[] mark_edge1 = MarkfaceTest[num1].GetEdges();
                                                    Edge[] mark_edge2 = MarkfaceTest[num2].GetEdges();
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                }
                                            }
                                            else if (MarkfaceTestpoint.Contains(Mapfacepoint[loc2]) == true)
                                            {
                                                double[] counts = MarkfaceTestpoint.ToArray();
                                                int count = counts.Length;
                                                int num2;
                                                num2 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc2], 0);
                                                int num1;
                                                num1 = loc1;
                                                if ((num1 < count) & (num1 != num2))
                                                {
                                                    Edge[] mark_edge1 = MarkfaceTest[num1].GetEdges();
                                                    Edge[] mark_edge2 = MarkfaceTest[num2].GetEdges();
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                }
                                            }
                                            #endregion
                                            #region 两个面均不是当前工序和映射工序的公共面,那么就按照顺序来
                                            else if ((MarkfaceTestpoint.Contains(Mapfacepoint[loc1]) == false) & (MarkfaceTestpoint.Contains(Mapfacepoint[loc2]) == false))
                                            {
                                                double[] counts = Markfacepoint.ToArray();
                                                int count = counts.Length;
                                                if ((loc1 < count) & (loc2 < count) & (loc1 != loc2))
                                                {
                                                    Edge[] mark_edge1 = Markface[loc1].GetEdges();
                                                    Edge[] mark_edge2 = Markface[loc2].GetEdges();
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                }
                                            }
                                            #endregion
                                        }
                                    }
                                }
                                #endregion
                            }
                            //还未试验成功
                            //既不是加工右边，也不是加工左边，而是对整体进行加工
                            else if ((start_name.Contains("right") == false) & (start_name.Contains("left")) == false)
                            {
                                #region 暂定与加工右边一样，用的是按X轴上的位置从大到小排列
                                theUFSession.Assem.SetWorkPart(map_part.Tag);
                                Part workPartMark = theSession.Parts.Work;
                                List<Face> Over_check_Face = new List<Face>();
                                Dimension[] map_dimension = workPartMark.Dimensions.ToArray();//所有尺寸
                                Face[] mark_face_in_target = Markface.ToArray();
                                int mark_face_num = mark_face_in_target.Length;
                                theUFSession.Assem.SetWorkPart(start_part.Tag);
                                workPartMark = theSession.Parts.Work;
                                Point3d position = new Point3d(-50, 70, 0);
                                foreach (Dimension check_dim in map_dimension)
                                {
                                    if (check_dim.GetType().ToString() == "NXOpen.Annotations.PmiParallelDimension")
                                    {
                                        Associativity ass1 = check_dim.GetAssociativity(1);
                                        Associativity ass2 = check_dim.GetAssociativity(2);
                                        Edge edge1 = (Edge)ass1.FirstObject;
                                        Edge edge2 = (Edge)ass2.FirstObject;
                                        //我们要找面
                                        Face[] face1 = edge1.GetFaces();
                                        Face[] face2 = edge2.GetFaces();
                                        int loc1 = 100;
                                        int loc2 = 100;
                                        #region 找标注有关的2个面在列表处的索引位置
                                        foreach (Face check_face1 in face1)
                                        {
                                            if (check_face1.SolidFaceType.ToString() == "Planar")
                                            {
                                                int check_index = 0;
                                                foreach (Face compare_face1 in Mapface)
                                                {
                                                    if (check_face1 == compare_face1)
                                                    {
                                                        loc1 = check_index;
                                                        break;
                                                    }
                                                    check_index = check_index + 1;
                                                }
                                            }
                                        }
                                        foreach (Face check_face2 in face2)
                                        {
                                            if (check_face2.SolidFaceType.ToString() == "Planar")
                                            {
                                                int check_index = 0;
                                                foreach (Face compare_face2 in Mapface)
                                                {
                                                    if (check_face2 == compare_face2)
                                                    {
                                                        loc2 = check_index;
                                                        break;
                                                    }
                                                    check_index = check_index + 1;
                                                }
                                            }
                                        }
                                        #endregion
                                        if ((loc1 == 100) || (loc2 == 100))
                                        {
                                            //即当前尺寸涉及的某个面在映射面中不存在
                                        }
                                        else
                                        {
                                            #region 当前标注的两个面当前工序和映射工序都存在
                                            //如果粗加工时，得到的两个面在映射标注的工序模型里都有
                                            if ((MarkfaceTestpoint.Contains(Mapfacepoint[loc1]) == true) & (MarkfaceTestpoint.Contains(Mapfacepoint[loc2]) == true))
                                            {
                                                int num1;
                                                num1 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc1], 0);
                                                int num2;
                                                num2 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc2], 0);
                                                if ((Markfacepoint.Contains(MarkfaceTestpoint[num1]) == false) & (Markfacepoint.Contains(MarkfaceTestpoint[num2]) == false))
                                                {
                                                    //当前标注的两个面，全部都不是制造特征体所得到的
                                                }
                                                else
                                                {
                                                    Edge[] mark_edge1 = MarkfaceTest[num1].GetEdges();
                                                    Edge[] mark_edge2 = MarkfaceTest[num2].GetEdges();
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                }
                                            }
                                            #endregion
                                            #region 当前标注有一个面是映射工序里存在着的
                                            //如果有一个面是当前工序得到的，而映射尺寸的工序模型里也有
                                            else if (MarkfaceTestpoint.Contains(Mapfacepoint[loc1]) == true)//只包含一个
                                            {
                                                double[] counts = MarkfaceTestpoint.ToArray();
                                                int count = counts.Length;
                                                int num1;
                                                num1 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc1], 0);
                                                int num2;
                                                num2 = loc2;
                                                if ((num2 < count) & (num1 != num2))
                                                {
                                                    Edge[] mark_edge1 = MarkfaceTest[num1].GetEdges();
                                                    Edge[] mark_edge2 = MarkfaceTest[num2].GetEdges();
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                }
                                            }
                                            else if (MarkfaceTestpoint.Contains(Mapfacepoint[loc2]) == true)
                                            {
                                                double[] counts = MarkfaceTestpoint.ToArray();
                                                int count = counts.Length;
                                                int num2;
                                                num2 = MarkfaceTestpoint.IndexOf(Mapfacepoint[loc2], 0);
                                                int num1;
                                                num1 = loc1;
                                                if ((num1 < count) & (num1 != num2))
                                                {
                                                    Edge[] mark_edge1 = MarkfaceTest[num1].GetEdges();
                                                    Edge[] mark_edge2 = MarkfaceTest[num2].GetEdges();
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                }
                                            }
                                            #endregion
                                            #region 两个面均不是当前工序和映射工序的公共面,那么就按照顺序来
                                            else if ((MarkfaceTestpoint.Contains(Mapfacepoint[loc1]) == false) & (MarkfaceTestpoint.Contains(Mapfacepoint[loc2]) == false))
                                            {
                                                double[] counts = Markfacepoint.ToArray();
                                                int count = counts.Length;
                                                if ((loc1 < count) & (loc2 < count) & (loc1 != loc2))
                                                {
                                                    Edge[] mark_edge1 = Markface[loc1].GetEdges();
                                                    Edge[] mark_edge2 = Markface[loc2].GetEdges();
                                                    Dimension markdim;
                                                    NXFun.createaxisdimension(mark_edge1[0], mark_edge2[0], out markdim);
                                                }
                                            }
                                            #endregion
                                        }
                                    }
                                }
                                #endregion
                            }
                            //未试验成功
                            #endregion
                        #endregion
                        }
                    }
                    #endregion
                }
            }
            foreach (string name in get_name)
            {
                if ((name.Contains("right") == true) || (name.Contains("left") == true))
                {
                    Part start_part = null;
                    start_part = (Part)theSession.Parts.FindObject(name);
                    theUFSession.Assem.SetWorkPart(start_part.Tag);
                    Part workPart = theSession.Parts.Work;
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
                    Body body = workPart.Bodies.ToArray()[0];
                    Dimension[] alldimension = workPart.Dimensions.ToArray();
                    double temp = 0;
                    int z = 10;
                    foreach (Dimension each in alldimension)
                    {
                        if (each.GetType().ToString() == "NXOpen.Annotations.PmiCylindricalDimension")
                        {
                            if (each.ComputedSize > temp)
                            {
                                temp = each.ComputedSize;
                            }
                        }
                    }

                    body = workPart.Bodies.ToArray()[0];
                    alldimension = workPart.Dimensions.ToArray();
                    strModelView = modelview.Name;
                    viewName = "BACK";
                    if (strModelView != viewName)
                    {
                        ModelingView modelingView1 = (ModelingView)workPart.ModelingViews.FindObject(viewName);
                        layout1.ReplaceView(workPart.ModelingViews.WorkView, modelingView1, true);
                    }
                    foreach (Dimension each in alldimension)
                    {
                        if (each.GetType().ToString() == "NXOpen.Annotations.PmiParallelDimension")
                        {
                            Point3d check_point1;
                            check_point1 = each.AnnotationOrigin;
                            check_point1.Z = temp + z;
                            each.AnnotationOrigin = check_point1;
                            each.IsOriginCentered = true;
                            z = z + 10;
                        }
                    }
                }
                PartLoadStatus partLoadStatus2;
                NXOpen.PartCollection.SdpsStatus status1;
                Part part1 = (Part)theSession.Parts.FindObject(mother);
                status1 = theSession.Parts.SetDisplay(part1, true, true, out partLoadStatus2);
                partLoadStatus2.Dispose();
            }
        }

        private void button2_Click(object sender, EventArgs e)//获取属性邻接图
        {//凸为1，凹为-1，不相连为0
            #region 单纯获得属性连接图
            Part workpart = theSession.Parts.Work;
            Body[] bodies = workpart.Bodies.ToArray();
            Face[] faces = bodies[0].GetFaces();
            int num=faces.Length;
            List<int[]> neigh_Face = new List<int[]>();
            for (int i = 0; i < num; i++)
            {
                int[] a=new int[num];
                neigh_Face.Add(a);
                int type1;
                double[] point1 = new double[6];
                double[] dir1 = new double[5];
                double[] box1 = new double[6];
                double radius1;
                double rad_data1;
                int norm_dir1;
                theUFSession.Modl.AskFaceData(faces[i].Tag,out type1,point1,dir1,box1,out radius1,out rad_data1,out norm_dir1);
                Vector3d vector1 = new Vector3d(dir1[0],dir1[1],dir1[2]);
                for (int j =i; j < num; j++)
                {
                    if (j == i)
                    {
                        neigh_Face[i][j] = 0;
                    }
                    else
                    {
                        Tag[] shared_edges;
                        theUFSession.Modl.AskSharedEdges(faces[i].Tag, faces[j].Tag, out shared_edges);
                        if (shared_edges.Length == 0)
                        {//无相交边
                            neigh_Face[i][j] = 0;
                        }
                        else if (shared_edges.Length != 0)
                        {
                            int type2;
                            double[] point2 = new double[6];
                            double[] dir2 = new double[5];
                            double[] box2 = new double[6];
                            double radius2;
                            double rad_data2;
                            int norm_dir2;
                            theUFSession.Modl.AskFaceData(faces[j].Tag, out type2, point2, dir2, box2, out radius2, out rad_data2, out norm_dir2);
                            Vector3d vector2 = new Vector3d(dir2[0], dir2[1], dir2[2]);
                            Vector3d result = new Vector3d(vector1.Y*vector2.Z-vector1.Z*vector2.Y,vector1.Z*vector2.X-vector1.X*vector2.Z,vector1.X*vector2.Y-vector1.Y*vector2.X);
                            double two = result.Y / (Math.Sqrt(Math.Pow(result.X,2)+Math.Pow(result.Y,2)+Math.Pow(result.Z,2)));
                            //用arccos不太靠谱
                            if (two>=0)
                            {//指向Y轴正方向
                                neigh_Face[i][j] = -1;
                            }
                            else if (two<0)
                            {//Y轴负方向
                                neigh_Face[i][j] = 1;
                            }
                            else
                            {
                                neigh_Face[i][j] = 1000;
                            }
                        }
                    }
                }
            }
            #endregion
            //#region 输出到listview
            ////输出到listview1中
            //listView1.Clear();
            //listView1.Columns.Add("面序号");
            //int b=listView1.Columns.Count;
            //if (b <= num+1)
            //{//我们添加了行英语表现序号
            //    for (int m = b; m <= num; m++)
            //    {
            //        listView1.Columns.Add(m.ToString());
            //    }
            //}
            //for (int i = 0; i < num; i++)
            //{
            //    ListViewItem Ivitem = listView1.Items.Add((i+1).ToString());//为了美观，不加1的话会从0开始，但不能直接把i加1
            //    for (int j = 0; j < num; j++)
            //    {
            //        //为了b
            //        neigh_Face[j][i] = neigh_Face[i][j];
            //        Ivitem.SubItems.Add(neigh_Face[i][j].ToString());
            //    }
            //}
            //#endregion 
            #region 存储到XML
            string name = workpart.JournalIdentifier;
            bool open = false;
            XmlDocument all = new XmlDocument();
            try
            {
                all.Load("d://属性邻接图.xml");
                open = true;
                NXFun.InputXml(name, neigh_Face, all);
            }
            catch
            {  
            }
            if (open == false)
            {
                XmlDeclaration decl = all.CreateXmlDeclaration("1.0", "utf-8", null);
                all.AppendChild(decl);
                XmlElement mother =all.CreateElement("零件名及其零件属性图");
                all.AppendChild(mother);
                NXFun.InputXml(name, neigh_Face, all);
            }
            #endregion
        }


        private void button5_Click(object sender, EventArgs e)//解决了圆柱的问题，圆柱统一改为这种标注方法，同时要对其进行筛选
        {
            #region 获取部件中各个part的名字
            int num_part;
            List<string> part_name = new List<string>();
            List<Tag> part_tag = new List<Tag>();
            //获得部件的个数
            num_part = theUFSession.Part.AskNumParts();
            //得到part的tag集
            for (int help_num_part = 0; help_num_part < num_part; help_num_part++)
            {
                part_tag.Add(theUFSession.Part.AskNthPart(help_num_part));
            }
            //得到part的name集
            foreach (Tag help_part in part_tag)
            {
                string help_name;
                theUFSession.Part.AskPartName(help_part, out help_name);
                Part help_part2 =(Part)theSession.Parts.FindObject(help_name);
                help_name = help_part2.JournalIdentifier;
                part_name.Add(help_name);//不再是路径了,直接是表中显示的名字，同时，findobject同样可以应用于这个非路径的名字
            }
            #endregion
            //获得整体
            string mother = null;
            foreach (string name in part_name)
            {
                if (name.Contains("Process") == true)
                {
                    mother = name;
                    break;
                }
            }

            string[] get_name=part_name.ToArray();
            foreach (string name in get_name)
            {
                if ((name.Contains("right") == true)||(name.Contains("left")==true))
                {
                    Part start_part = null;
                    start_part = (Part)theSession.Parts.FindObject(name);
                    theUFSession.Assem.SetWorkPart(start_part.Tag);
                    Part workPart = theSession.Parts.Work;
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
                    Body body = workPart.Bodies.ToArray()[0];
                    Dimension[] alldimension = workPart.Dimensions.ToArray();
                    double temp = 0;
                    int z = 10;
                    foreach (Dimension each in alldimension)
                    {
                        if (each.GetType().ToString() == "NXOpen.Annotations.PmiCylindricalDimension")
                        {
                            if (each.ComputedSize > temp)
                            {
                                temp = each.ComputedSize;
                            }
                        }
                    }

                    body = workPart.Bodies.ToArray()[0];
                    alldimension = workPart.Dimensions.ToArray();
                    strModelView = modelview.Name;
                    viewName = "BACK";
                    if (strModelView != viewName)
                    {
                        ModelingView modelingView1 = (ModelingView)workPart.ModelingViews.FindObject(viewName);
                        layout1.ReplaceView(workPart.ModelingViews.WorkView, modelingView1, true);
                    }
                    foreach (Dimension each in alldimension)
                    {
                        if (each.GetType().ToString() == "NXOpen.Annotations.PmiParallelDimension")
                        {
                            Point3d check_point1;
                            check_point1 = each.AnnotationOrigin;
                            check_point1.Z = temp + z;
                            each.AnnotationOrigin = check_point1;
                            each.IsOriginCentered = true;
                            z = z + 10;
                        }
                    }
                }
                PartLoadStatus partLoadStatus2;
                NXOpen.PartCollection.SdpsStatus status1;
                Part part1 = (Part)theSession.Parts.FindObject(mother);
                status1 = theSession.Parts.SetDisplay(part1, true, true, out partLoadStatus2);
                partLoadStatus2.Dispose();
            }
        }
    }
}
