using System;
using System.Buffers;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Xunit;

using static Cesil.Tests.Helpers;

namespace Cesil.Tests
{
    public class DynamicWriterTests
    {
        private static dynamic MakeDynamicRow(string csvStr)
        {
            var opts =
                Options.CreateBuilder(Options.Default)
                    .WithReadHeader(ReadHeader.Always)
                    .WithDynamicRowDisposal(DynamicRowDisposal.OnExplicitDispose)
                    .ToOptions();

            var config = Configuration.ForDynamic(opts);

            using (var reader = new StringReader(csvStr))
            using (var csv = config.CreateReader(reader))
            {
                Assert.True(csv.TryRead(out var ret));
                Assert.False(csv.TryRead(out _));

                return ret;
            }
        }

        [Fact]
        public void WriteRange()
        {
            var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Never).ToOptions();

            RunSyncDynamicWriterVariants(
                opts,
                (config, getWriter, getStr) =>
                {
                    var row = MakeDynamicRow($"A,B,C,D\r\n1,2,3,4");
                    var range1 = row[1..^1];
                    var range2 = row[1..];

                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.Write(range1);
                        csv.Write(range2);
                    }

                    var res = getStr();
                    Assert.Equal("2,3\r\n2,3,4", res);

                    row.Dispose();
                    range1.Dispose();
                    range2.Dispose();
                }
            );
        }

        [Fact]
        public void WriteCommentErrors()
        {
            RunSyncDynamicWriterVariants(
                Options.DynamicDefault,
                (config, getWriter, getStr) =>
                {
                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        var exc = Assert.Throws<InvalidOperationException>(() => csv.WriteComment("foo"));
                        Assert.Equal($"No {nameof(Options.CommentCharacter)} configured, cannot write a comment line", exc.Message);
                    }

                    getStr();
                }
            );
        }

        [Fact]
        public void IllegalRowSizes()
        {
            RunSyncDynamicWriterVariants(
                Options.DynamicDefault,         // this only happens if you're writing headers, which we do by default
                (config, getWriter, getStr) =>
                {
                    var row = MakeDynamicRow("A,B,C\r\n1,2,3");
                    var tooBigRow = MakeDynamicRow("A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q,R,S,T,U,V\r\n4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25");

                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.Write(row);

                        var exc = Assert.Throws<InvalidOperationException>(() => { csv.Write(tooBigRow); });
                        Assert.Equal("Too many cells returned, could not place in desired order", exc.Message);
                    }

                    getStr();
                }
            );
        }

        [Fact]
        public void VariableSizeRows()
        {
            var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Never).ToOptions();

            // smaller first
            RunSyncDynamicWriterVariants(
                opts,
                (config, getWriter, getStr) =>
                {
                    var r1 = MakeDynamicRow("A,B,C\r\n1,2,3");
                    var r2 = MakeDynamicRow("A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q,R,S,T,U,V\r\n4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25");

                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.Write(r1);
                        csv.Write(r2);
                    }

                    var res = getStr();
                    Assert.Equal("1,2,3\r\n4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25", res);
                }
            );

            // larger first
            RunSyncDynamicWriterVariants(
                opts,
                (config, getWriter, getStr) =>
                {
                    var r1 = MakeDynamicRow("A,B,C\r\n1,2,3");
                    var r2 = MakeDynamicRow("A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q,R,S,T,U,V\r\n4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25");

                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.Write(r2);
                        csv.Write(r1);
                    }

                    var res = getStr();
                    Assert.Equal("4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25\r\n1,2,3", res);
                }
            );
        }

        [Fact]
        public void ReturnedRowCounts()
        {
            // simple
            RunSyncDynamicWriterVariants(
                Options.DynamicDefault,
                (config, getWriter, getStr) =>
                {
                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        var n1 =
                            csv.WriteAll(
                                new[]
                                {
                                    MakeDynamicRow("A,B\r\nFoo,123"),
                                    MakeDynamicRow("A,B\r\nBar,456"),
                                    MakeDynamicRow("A,B\r\nFizz,456")
                                }
                            );

                        Assert.Equal(3, n1);

                        var n2 =
                            csv.WriteAll(
                                new[]
                                {
                                    MakeDynamicRow("A,B\r\nBuzz,789")
                                }
                            );

                        Assert.Equal(1, n2);
                    }

                    var res = getStr();
                    Assert.Equal("A,B\r\nFoo,123\r\nBar,456\r\nFizz,456\r\nBuzz,789", res);
                }
            );

            // no count
            RunSyncDynamicWriterVariants(
                Options.DynamicDefault,
                (config, getWriter, getStr) =>
                {
                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        var n1 =
                            csv.WriteAll(
                                Enumerable.Range(0, 1_000).Select(x => MakeDynamicRow($"Item1,Item2\r\n{x},{x * 2}"))
                            );

                        Assert.Equal(1_000, n1);

                        var n2 =
                            csv.WriteAll(
                                new[] { MakeDynamicRow($"Item1,Item2\r\nFoo,-1") }
                            );
                        Assert.Equal(1, n2);
                    }

                    var res = getStr();
                    Assert.Equal("Item1,Item2\r\n0,0\r\n1,2\r\n2,4\r\n3,6\r\n4,8\r\n5,10\r\n6,12\r\n7,14\r\n8,16\r\n9,18\r\n10,20\r\n11,22\r\n12,24\r\n13,26\r\n14,28\r\n15,30\r\n16,32\r\n17,34\r\n18,36\r\n19,38\r\n20,40\r\n21,42\r\n22,44\r\n23,46\r\n24,48\r\n25,50\r\n26,52\r\n27,54\r\n28,56\r\n29,58\r\n30,60\r\n31,62\r\n32,64\r\n33,66\r\n34,68\r\n35,70\r\n36,72\r\n37,74\r\n38,76\r\n39,78\r\n40,80\r\n41,82\r\n42,84\r\n43,86\r\n44,88\r\n45,90\r\n46,92\r\n47,94\r\n48,96\r\n49,98\r\n50,100\r\n51,102\r\n52,104\r\n53,106\r\n54,108\r\n55,110\r\n56,112\r\n57,114\r\n58,116\r\n59,118\r\n60,120\r\n61,122\r\n62,124\r\n63,126\r\n64,128\r\n65,130\r\n66,132\r\n67,134\r\n68,136\r\n69,138\r\n70,140\r\n71,142\r\n72,144\r\n73,146\r\n74,148\r\n75,150\r\n76,152\r\n77,154\r\n78,156\r\n79,158\r\n80,160\r\n81,162\r\n82,164\r\n83,166\r\n84,168\r\n85,170\r\n86,172\r\n87,174\r\n88,176\r\n89,178\r\n90,180\r\n91,182\r\n92,184\r\n93,186\r\n94,188\r\n95,190\r\n96,192\r\n97,194\r\n98,196\r\n99,198\r\n100,200\r\n101,202\r\n102,204\r\n103,206\r\n104,208\r\n105,210\r\n106,212\r\n107,214\r\n108,216\r\n109,218\r\n110,220\r\n111,222\r\n112,224\r\n113,226\r\n114,228\r\n115,230\r\n116,232\r\n117,234\r\n118,236\r\n119,238\r\n120,240\r\n121,242\r\n122,244\r\n123,246\r\n124,248\r\n125,250\r\n126,252\r\n127,254\r\n128,256\r\n129,258\r\n130,260\r\n131,262\r\n132,264\r\n133,266\r\n134,268\r\n135,270\r\n136,272\r\n137,274\r\n138,276\r\n139,278\r\n140,280\r\n141,282\r\n142,284\r\n143,286\r\n144,288\r\n145,290\r\n146,292\r\n147,294\r\n148,296\r\n149,298\r\n150,300\r\n151,302\r\n152,304\r\n153,306\r\n154,308\r\n155,310\r\n156,312\r\n157,314\r\n158,316\r\n159,318\r\n160,320\r\n161,322\r\n162,324\r\n163,326\r\n164,328\r\n165,330\r\n166,332\r\n167,334\r\n168,336\r\n169,338\r\n170,340\r\n171,342\r\n172,344\r\n173,346\r\n174,348\r\n175,350\r\n176,352\r\n177,354\r\n178,356\r\n179,358\r\n180,360\r\n181,362\r\n182,364\r\n183,366\r\n184,368\r\n185,370\r\n186,372\r\n187,374\r\n188,376\r\n189,378\r\n190,380\r\n191,382\r\n192,384\r\n193,386\r\n194,388\r\n195,390\r\n196,392\r\n197,394\r\n198,396\r\n199,398\r\n200,400\r\n201,402\r\n202,404\r\n203,406\r\n204,408\r\n205,410\r\n206,412\r\n207,414\r\n208,416\r\n209,418\r\n210,420\r\n211,422\r\n212,424\r\n213,426\r\n214,428\r\n215,430\r\n216,432\r\n217,434\r\n218,436\r\n219,438\r\n220,440\r\n221,442\r\n222,444\r\n223,446\r\n224,448\r\n225,450\r\n226,452\r\n227,454\r\n228,456\r\n229,458\r\n230,460\r\n231,462\r\n232,464\r\n233,466\r\n234,468\r\n235,470\r\n236,472\r\n237,474\r\n238,476\r\n239,478\r\n240,480\r\n241,482\r\n242,484\r\n243,486\r\n244,488\r\n245,490\r\n246,492\r\n247,494\r\n248,496\r\n249,498\r\n250,500\r\n251,502\r\n252,504\r\n253,506\r\n254,508\r\n255,510\r\n256,512\r\n257,514\r\n258,516\r\n259,518\r\n260,520\r\n261,522\r\n262,524\r\n263,526\r\n264,528\r\n265,530\r\n266,532\r\n267,534\r\n268,536\r\n269,538\r\n270,540\r\n271,542\r\n272,544\r\n273,546\r\n274,548\r\n275,550\r\n276,552\r\n277,554\r\n278,556\r\n279,558\r\n280,560\r\n281,562\r\n282,564\r\n283,566\r\n284,568\r\n285,570\r\n286,572\r\n287,574\r\n288,576\r\n289,578\r\n290,580\r\n291,582\r\n292,584\r\n293,586\r\n294,588\r\n295,590\r\n296,592\r\n297,594\r\n298,596\r\n299,598\r\n300,600\r\n301,602\r\n302,604\r\n303,606\r\n304,608\r\n305,610\r\n306,612\r\n307,614\r\n308,616\r\n309,618\r\n310,620\r\n311,622\r\n312,624\r\n313,626\r\n314,628\r\n315,630\r\n316,632\r\n317,634\r\n318,636\r\n319,638\r\n320,640\r\n321,642\r\n322,644\r\n323,646\r\n324,648\r\n325,650\r\n326,652\r\n327,654\r\n328,656\r\n329,658\r\n330,660\r\n331,662\r\n332,664\r\n333,666\r\n334,668\r\n335,670\r\n336,672\r\n337,674\r\n338,676\r\n339,678\r\n340,680\r\n341,682\r\n342,684\r\n343,686\r\n344,688\r\n345,690\r\n346,692\r\n347,694\r\n348,696\r\n349,698\r\n350,700\r\n351,702\r\n352,704\r\n353,706\r\n354,708\r\n355,710\r\n356,712\r\n357,714\r\n358,716\r\n359,718\r\n360,720\r\n361,722\r\n362,724\r\n363,726\r\n364,728\r\n365,730\r\n366,732\r\n367,734\r\n368,736\r\n369,738\r\n370,740\r\n371,742\r\n372,744\r\n373,746\r\n374,748\r\n375,750\r\n376,752\r\n377,754\r\n378,756\r\n379,758\r\n380,760\r\n381,762\r\n382,764\r\n383,766\r\n384,768\r\n385,770\r\n386,772\r\n387,774\r\n388,776\r\n389,778\r\n390,780\r\n391,782\r\n392,784\r\n393,786\r\n394,788\r\n395,790\r\n396,792\r\n397,794\r\n398,796\r\n399,798\r\n400,800\r\n401,802\r\n402,804\r\n403,806\r\n404,808\r\n405,810\r\n406,812\r\n407,814\r\n408,816\r\n409,818\r\n410,820\r\n411,822\r\n412,824\r\n413,826\r\n414,828\r\n415,830\r\n416,832\r\n417,834\r\n418,836\r\n419,838\r\n420,840\r\n421,842\r\n422,844\r\n423,846\r\n424,848\r\n425,850\r\n426,852\r\n427,854\r\n428,856\r\n429,858\r\n430,860\r\n431,862\r\n432,864\r\n433,866\r\n434,868\r\n435,870\r\n436,872\r\n437,874\r\n438,876\r\n439,878\r\n440,880\r\n441,882\r\n442,884\r\n443,886\r\n444,888\r\n445,890\r\n446,892\r\n447,894\r\n448,896\r\n449,898\r\n450,900\r\n451,902\r\n452,904\r\n453,906\r\n454,908\r\n455,910\r\n456,912\r\n457,914\r\n458,916\r\n459,918\r\n460,920\r\n461,922\r\n462,924\r\n463,926\r\n464,928\r\n465,930\r\n466,932\r\n467,934\r\n468,936\r\n469,938\r\n470,940\r\n471,942\r\n472,944\r\n473,946\r\n474,948\r\n475,950\r\n476,952\r\n477,954\r\n478,956\r\n479,958\r\n480,960\r\n481,962\r\n482,964\r\n483,966\r\n484,968\r\n485,970\r\n486,972\r\n487,974\r\n488,976\r\n489,978\r\n490,980\r\n491,982\r\n492,984\r\n493,986\r\n494,988\r\n495,990\r\n496,992\r\n497,994\r\n498,996\r\n499,998\r\n500,1000\r\n501,1002\r\n502,1004\r\n503,1006\r\n504,1008\r\n505,1010\r\n506,1012\r\n507,1014\r\n508,1016\r\n509,1018\r\n510,1020\r\n511,1022\r\n512,1024\r\n513,1026\r\n514,1028\r\n515,1030\r\n516,1032\r\n517,1034\r\n518,1036\r\n519,1038\r\n520,1040\r\n521,1042\r\n522,1044\r\n523,1046\r\n524,1048\r\n525,1050\r\n526,1052\r\n527,1054\r\n528,1056\r\n529,1058\r\n530,1060\r\n531,1062\r\n532,1064\r\n533,1066\r\n534,1068\r\n535,1070\r\n536,1072\r\n537,1074\r\n538,1076\r\n539,1078\r\n540,1080\r\n541,1082\r\n542,1084\r\n543,1086\r\n544,1088\r\n545,1090\r\n546,1092\r\n547,1094\r\n548,1096\r\n549,1098\r\n550,1100\r\n551,1102\r\n552,1104\r\n553,1106\r\n554,1108\r\n555,1110\r\n556,1112\r\n557,1114\r\n558,1116\r\n559,1118\r\n560,1120\r\n561,1122\r\n562,1124\r\n563,1126\r\n564,1128\r\n565,1130\r\n566,1132\r\n567,1134\r\n568,1136\r\n569,1138\r\n570,1140\r\n571,1142\r\n572,1144\r\n573,1146\r\n574,1148\r\n575,1150\r\n576,1152\r\n577,1154\r\n578,1156\r\n579,1158\r\n580,1160\r\n581,1162\r\n582,1164\r\n583,1166\r\n584,1168\r\n585,1170\r\n586,1172\r\n587,1174\r\n588,1176\r\n589,1178\r\n590,1180\r\n591,1182\r\n592,1184\r\n593,1186\r\n594,1188\r\n595,1190\r\n596,1192\r\n597,1194\r\n598,1196\r\n599,1198\r\n600,1200\r\n601,1202\r\n602,1204\r\n603,1206\r\n604,1208\r\n605,1210\r\n606,1212\r\n607,1214\r\n608,1216\r\n609,1218\r\n610,1220\r\n611,1222\r\n612,1224\r\n613,1226\r\n614,1228\r\n615,1230\r\n616,1232\r\n617,1234\r\n618,1236\r\n619,1238\r\n620,1240\r\n621,1242\r\n622,1244\r\n623,1246\r\n624,1248\r\n625,1250\r\n626,1252\r\n627,1254\r\n628,1256\r\n629,1258\r\n630,1260\r\n631,1262\r\n632,1264\r\n633,1266\r\n634,1268\r\n635,1270\r\n636,1272\r\n637,1274\r\n638,1276\r\n639,1278\r\n640,1280\r\n641,1282\r\n642,1284\r\n643,1286\r\n644,1288\r\n645,1290\r\n646,1292\r\n647,1294\r\n648,1296\r\n649,1298\r\n650,1300\r\n651,1302\r\n652,1304\r\n653,1306\r\n654,1308\r\n655,1310\r\n656,1312\r\n657,1314\r\n658,1316\r\n659,1318\r\n660,1320\r\n661,1322\r\n662,1324\r\n663,1326\r\n664,1328\r\n665,1330\r\n666,1332\r\n667,1334\r\n668,1336\r\n669,1338\r\n670,1340\r\n671,1342\r\n672,1344\r\n673,1346\r\n674,1348\r\n675,1350\r\n676,1352\r\n677,1354\r\n678,1356\r\n679,1358\r\n680,1360\r\n681,1362\r\n682,1364\r\n683,1366\r\n684,1368\r\n685,1370\r\n686,1372\r\n687,1374\r\n688,1376\r\n689,1378\r\n690,1380\r\n691,1382\r\n692,1384\r\n693,1386\r\n694,1388\r\n695,1390\r\n696,1392\r\n697,1394\r\n698,1396\r\n699,1398\r\n700,1400\r\n701,1402\r\n702,1404\r\n703,1406\r\n704,1408\r\n705,1410\r\n706,1412\r\n707,1414\r\n708,1416\r\n709,1418\r\n710,1420\r\n711,1422\r\n712,1424\r\n713,1426\r\n714,1428\r\n715,1430\r\n716,1432\r\n717,1434\r\n718,1436\r\n719,1438\r\n720,1440\r\n721,1442\r\n722,1444\r\n723,1446\r\n724,1448\r\n725,1450\r\n726,1452\r\n727,1454\r\n728,1456\r\n729,1458\r\n730,1460\r\n731,1462\r\n732,1464\r\n733,1466\r\n734,1468\r\n735,1470\r\n736,1472\r\n737,1474\r\n738,1476\r\n739,1478\r\n740,1480\r\n741,1482\r\n742,1484\r\n743,1486\r\n744,1488\r\n745,1490\r\n746,1492\r\n747,1494\r\n748,1496\r\n749,1498\r\n750,1500\r\n751,1502\r\n752,1504\r\n753,1506\r\n754,1508\r\n755,1510\r\n756,1512\r\n757,1514\r\n758,1516\r\n759,1518\r\n760,1520\r\n761,1522\r\n762,1524\r\n763,1526\r\n764,1528\r\n765,1530\r\n766,1532\r\n767,1534\r\n768,1536\r\n769,1538\r\n770,1540\r\n771,1542\r\n772,1544\r\n773,1546\r\n774,1548\r\n775,1550\r\n776,1552\r\n777,1554\r\n778,1556\r\n779,1558\r\n780,1560\r\n781,1562\r\n782,1564\r\n783,1566\r\n784,1568\r\n785,1570\r\n786,1572\r\n787,1574\r\n788,1576\r\n789,1578\r\n790,1580\r\n791,1582\r\n792,1584\r\n793,1586\r\n794,1588\r\n795,1590\r\n796,1592\r\n797,1594\r\n798,1596\r\n799,1598\r\n800,1600\r\n801,1602\r\n802,1604\r\n803,1606\r\n804,1608\r\n805,1610\r\n806,1612\r\n807,1614\r\n808,1616\r\n809,1618\r\n810,1620\r\n811,1622\r\n812,1624\r\n813,1626\r\n814,1628\r\n815,1630\r\n816,1632\r\n817,1634\r\n818,1636\r\n819,1638\r\n820,1640\r\n821,1642\r\n822,1644\r\n823,1646\r\n824,1648\r\n825,1650\r\n826,1652\r\n827,1654\r\n828,1656\r\n829,1658\r\n830,1660\r\n831,1662\r\n832,1664\r\n833,1666\r\n834,1668\r\n835,1670\r\n836,1672\r\n837,1674\r\n838,1676\r\n839,1678\r\n840,1680\r\n841,1682\r\n842,1684\r\n843,1686\r\n844,1688\r\n845,1690\r\n846,1692\r\n847,1694\r\n848,1696\r\n849,1698\r\n850,1700\r\n851,1702\r\n852,1704\r\n853,1706\r\n854,1708\r\n855,1710\r\n856,1712\r\n857,1714\r\n858,1716\r\n859,1718\r\n860,1720\r\n861,1722\r\n862,1724\r\n863,1726\r\n864,1728\r\n865,1730\r\n866,1732\r\n867,1734\r\n868,1736\r\n869,1738\r\n870,1740\r\n871,1742\r\n872,1744\r\n873,1746\r\n874,1748\r\n875,1750\r\n876,1752\r\n877,1754\r\n878,1756\r\n879,1758\r\n880,1760\r\n881,1762\r\n882,1764\r\n883,1766\r\n884,1768\r\n885,1770\r\n886,1772\r\n887,1774\r\n888,1776\r\n889,1778\r\n890,1780\r\n891,1782\r\n892,1784\r\n893,1786\r\n894,1788\r\n895,1790\r\n896,1792\r\n897,1794\r\n898,1796\r\n899,1798\r\n900,1800\r\n901,1802\r\n902,1804\r\n903,1806\r\n904,1808\r\n905,1810\r\n906,1812\r\n907,1814\r\n908,1816\r\n909,1818\r\n910,1820\r\n911,1822\r\n912,1824\r\n913,1826\r\n914,1828\r\n915,1830\r\n916,1832\r\n917,1834\r\n918,1836\r\n919,1838\r\n920,1840\r\n921,1842\r\n922,1844\r\n923,1846\r\n924,1848\r\n925,1850\r\n926,1852\r\n927,1854\r\n928,1856\r\n929,1858\r\n930,1860\r\n931,1862\r\n932,1864\r\n933,1866\r\n934,1868\r\n935,1870\r\n936,1872\r\n937,1874\r\n938,1876\r\n939,1878\r\n940,1880\r\n941,1882\r\n942,1884\r\n943,1886\r\n944,1888\r\n945,1890\r\n946,1892\r\n947,1894\r\n948,1896\r\n949,1898\r\n950,1900\r\n951,1902\r\n952,1904\r\n953,1906\r\n954,1908\r\n955,1910\r\n956,1912\r\n957,1914\r\n958,1916\r\n959,1918\r\n960,1920\r\n961,1922\r\n962,1924\r\n963,1926\r\n964,1928\r\n965,1930\r\n966,1932\r\n967,1934\r\n968,1936\r\n969,1938\r\n970,1940\r\n971,1942\r\n972,1944\r\n973,1946\r\n974,1948\r\n975,1950\r\n976,1952\r\n977,1954\r\n978,1956\r\n979,1958\r\n980,1960\r\n981,1962\r\n982,1964\r\n983,1966\r\n984,1968\r\n985,1970\r\n986,1972\r\n987,1974\r\n988,1976\r\n989,1978\r\n990,1980\r\n991,1982\r\n992,1984\r\n993,1986\r\n994,1988\r\n995,1990\r\n996,1992\r\n997,1994\r\n998,1996\r\n999,1998\r\nFoo,-1", res);
                }
            );
        }

        [Fact]
        public void MultiCharacterValueSeparators()
        {
            var opts = Options.CreateBuilder(Options.DynamicDefault).WithValueSeparator("#*#").ToOptions();

            // no escapes
            {
                var r1 = MakeDynamicRow("A,B\r\n123,foo");
                var r2 = MakeDynamicRow("A,B\r\n456,#");
                var r3 = MakeDynamicRow("A,B\r\n789,*");

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(r1);
                            csv.Write(r2);
                            csv.Write(r3);
                        }

                        var res = getStr();
                        Assert.Equal("A#*#B\r\n123#*#foo\r\n456#*##\r\n789#*#*", res);
                    }
                );
            }

            // escapes
            {
                var r1 = MakeDynamicRow("A,B\r\n123,foo#*#bar");
                var r2 = MakeDynamicRow("A,B\r\n456,#");
                var r3 = MakeDynamicRow("A,B\r\n789,*");

                RunSyncDynamicWriterVariants(
                   opts,
                   (config, getWriter, getStr) =>
                   {
                       using (var writer = getWriter())
                       using (var csv = config.CreateWriter(writer))
                       {
                           csv.Write(r1);
                           csv.Write(r2);
                           csv.Write(r3);
                       }

                       var res = getStr();
                       Assert.Equal("A#*#B\r\n123#*#\"foo#*#bar\"\r\n456#*##\r\n789#*#*", res);
                   }
               );
            }

            // in headers
            {
                var r1 = MakeDynamicRow("A#*#Escaped,B\r\n123,foo#*#bar");
                var r2 = MakeDynamicRow("A#*#Escaped,B\r\n456,#");
                var r3 = MakeDynamicRow("A#*#Escaped,B\r\n789,*");

                RunSyncDynamicWriterVariants(
                  opts,
                  (config, getWriter, getStr) =>
                  {
                      using (var writer = getWriter())
                      using (var csv = config.CreateWriter(writer))
                      {
                          csv.Write(r1);
                          csv.Write(r2);
                          csv.Write(r3);
                      }

                      var res = getStr();
                      Assert.Equal("\"A#*#Escaped\"#*#B\r\n123#*#\"foo#*#bar\"\r\n456#*##\r\n789#*#*", res);
                  }
              );
            }
        }

        [Fact]
        public void WriteCommentBeforeRow()
        {
            // no headers
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Never).WithCommentCharacter('#').ToOptions();
                // comment, then row
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteComment("hello world");
                            csv.Write(new { Foo = 123, Bar = "+456" });
                        }

                        var res = getStr();
                        Assert.Equal("#hello world\r\n123,+456", res);
                    }
                );

                // comment, no row
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteComment("hello world");
                        }

                        var res = getStr();
                        Assert.Equal("#hello world", res);
                    }
                );

                // multiple comments, then row
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteComment("hello world");
                            csv.WriteComment("fizz buzz");
                            csv.WriteComment("foo\r\nbar");
                            csv.Write(new { Foo = 123, Bar = "+456" });
                        }

                        var res = getStr();
                        Assert.Equal("#hello world\r\n#fizz buzz\r\n#foo\r\n#bar\r\n123,+456", res);
                    }
                );

                // multiple comments, no row
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteComment("hello world");
                            csv.WriteComment("fizz buzz");
                            csv.WriteComment("foo\r\nbar");
                        }

                        var res = getStr();
                        Assert.Equal("#hello world\r\n#fizz buzz\r\n#foo\r\n#bar", res);
                    }
                );
            }

            // headers
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithCommentCharacter('#').ToOptions();
                // comment, then row
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteComment("hello world");
                            csv.Write(new { Foo = 123, Bar = "+456" });
                        }

                        var res = getStr();
                        Assert.Equal("#hello world\r\nFoo,Bar\r\n123,+456", res);
                    }
                );

                // comment, no row
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteComment("hello world");
                        }

                        var res = getStr();
                        Assert.Equal("#hello world", res);
                    }
                );

                // multiple comments, then row
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteComment("hello world");
                            csv.WriteComment("fizz buzz");
                            csv.WriteComment("foo\r\nbar");
                            csv.Write(new { Foo = 123, Bar = "+456" });
                        }

                        var res = getStr();
                        Assert.Equal("#hello world\r\n#fizz buzz\r\n#foo\r\n#bar\r\nFoo,Bar\r\n123,+456", res);
                    }
                );

                // multiple comments, no row
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteComment("hello world");
                            csv.WriteComment("fizz buzz");
                            csv.WriteComment("foo\r\nbar");
                        }

                        var res = getStr();
                        Assert.Equal("#hello world\r\n#fizz buzz\r\n#foo\r\n#bar", res);
                    }
                );
            }
        }

        private sealed class _ChainedFormatters_Context
        {
            public int F { get; set; }
        }

        private sealed class _ChainedFormatters_TypeDescriber : DefaultTypeDescriber
        {
            private readonly Formatter F;

            public _ChainedFormatters_TypeDescriber(Formatter f)
            {
                F = f;
            }

            public override int GetCellsForDynamicRow(in WriteContext context, dynamic row, Span<DynamicCellValue> cells)
            {
                if (cells.Length < 1)
                {
                    return 1;
                }

                string val = row.Foo;

                var ret = DynamicCellValue.Create("Foo", val, F);

                cells[0] = ret;
                return 1;
            }
        }

        [Fact]
        public void ChainedFormatters()
        {
            var f1 =
                Formatter.ForDelegate<string>(
                    (string data, in WriteContext ctx, IBufferWriter<char> writer) =>
                    {
                        var num = ((_ChainedFormatters_Context)ctx.Context).F;

                        if (num != 1) return false;

                        var span = writer.GetSpan(4);
                        span[0] = '1';
                        span[1] = '2';
                        span[2] = '3';
                        span[3] = '4';

                        writer.Advance(4);

                        return true;
                    }
                );
            var f2 =
                Formatter.ForDelegate<string>(
                    (string data, in WriteContext ctx, IBufferWriter<char> writer) =>
                    {
                        var num = ((_ChainedFormatters_Context)ctx.Context).F;

                        if (num != 2) return false;

                        var span = writer.GetSpan(3);
                        span[0] = 'a';
                        span[1] = 'b';
                        span[2] = 'c';

                        writer.Advance(3);

                        return true;
                    }
                );
            var f3 =
                Formatter.ForDelegate<string>(
                    (string data, in WriteContext ctx, IBufferWriter<char> writer) =>
                    {
                        var num = ((_ChainedFormatters_Context)ctx.Context).F;

                        if (num != 3) return false;

                        var span = writer.GetSpan(2);
                        span[0] = '0';
                        span[1] = '0';

                        writer.Advance(2);

                        return true;
                    }
                );

            var f = f1.Else(f2).Else(f3);

            var td = new _ChainedFormatters_TypeDescriber(f);

            var opts = OptionsBuilder.CreateBuilder(Options.DynamicDefault).WithTypeDescriber(td).ToOptions();



            var row = MakeDynamicRow("Foo\r\nabc");
            try
            {
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        var ctx = new _ChainedFormatters_Context();

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer, ctx))
                        {
                            ctx.F = 1;
                            csv.Write(row);
                            ctx.F = 2;
                            csv.Write(row);
                            ctx.F = 3;
                            csv.Write(row);
                            ctx.F = 1;
                            csv.Write(row);
                        }

                        var str = getStr();
                        Assert.Equal("Foo\r\n1234\r\nabc\r\n00\r\n1234", str);
                    }
                );
            }
            finally
            {
                row.Dispose();
            }
        }

        [Fact]
        public void NoEscapes()
        {
            // no escapes at all (TSV)
            {
                var opts = OptionsBuilder.CreateBuilder(Options.DynamicDefault).WithValueSeparator("\t").WithEscapedValueStartAndEnd(null).WithEscapedValueEscapeCharacter(null).ToOptions();

                // correct
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(new { Foo = "abc", Bar = "123" });
                            csv.Write(new { Foo = "\"", Bar = "," });
                        }

                        var str = getStr();
                        Assert.Equal("Foo\tBar\r\nabc\t123\r\n\"\t,", str);
                    }
                );

                // explodes if there's an value separator in a value, since there are no escapes
                {
                    // \t
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                var inv = Assert.Throws<InvalidOperationException>(() => csv.Write(new { Foo = "\t", Bar = "foo" }));

                                Assert.Equal("Tried to write a value contain '\t' which requires escaping a value, but no way to escape a value is configured", inv.Message);
                            }

                            getStr();
                        }
                    );

                    // \r\n
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                var inv = Assert.Throws<InvalidOperationException>(() => csv.Write(new { Foo = "foo", Bar = "\r\n" }));

                                Assert.Equal("Tried to write a value contain '\r' which requires escaping a value, but no way to escape a value is configured", inv.Message);
                            }

                            getStr();
                        }
                    );

                    var optsWithComment = OptionsBuilder.CreateBuilder(opts).WithCommentCharacter('#').ToOptions();
                    // #
                    RunSyncDynamicWriterVariants(
                        optsWithComment,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                var inv = Assert.Throws<InvalidOperationException>(() => csv.Write(new { Foo = "#", Bar = "fizz" }));

                                Assert.Equal("Tried to write a value contain '#' which requires escaping a value, but no way to escape a value is configured", inv.Message);
                            }

                            getStr();
                        }
                    );
                }
            }

            // escapes, but no escape for the escape start and end char
            {
                var opts = OptionsBuilder.CreateBuilder(Options.DynamicDefault).WithValueSeparator("\t").WithEscapedValueStartAndEnd('"').WithEscapedValueEscapeCharacter(null).ToOptions();

                // correct
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(new { Foo = "a\tbc", Bar = "#123" });
                            csv.Write(new { Foo = "\r", Bar = "," });
                        }

                        var str = getStr();
                        Assert.Equal("Foo\tBar\r\n\"a\tbc\"\t#123\r\n\"\r\"\t,", str);
                    }
                );

                var optsWithComments = OptionsBuilder.CreateBuilder(opts).WithCommentCharacter('#').ToOptions();

                // correct with comments
                RunSyncDynamicWriterVariants(
                    optsWithComments,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(new { Foo = "a\tbc", Bar = "#123" });
                            csv.Write(new { Foo = "\r", Bar = "," });
                        }

                        var str = getStr();
                        Assert.Equal("Foo\tBar\r\n\"a\tbc\"\t\"#123\"\r\n\"\r\"\t,", str);
                    }
                );

                // explodes if there's an escape start character in a value, since it can't be escaped
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            var inv = Assert.Throws<InvalidOperationException>(() => csv.Write(new { Foo = "a\tbc", Bar = "\"" }));

                            Assert.Equal("Tried to write a value contain '\"' which requires escaping the character in an escaped value, but no way to escape inside an escaped value is configured", inv.Message);
                        }

                        getStr();
                    }
                );
            }
        }

        [Fact]
        public void NullComment()
        {
            RunSyncDynamicWriterVariants(
                Options.DynamicDefault,
                (config, getWriter, getStr) =>
                {
                    using (var w = getWriter())
                    using (var csv = config.CreateWriter(w))
                    {
                        Assert.Throws<ArgumentNullException>(() => csv.WriteComment(null));
                    }

                    var res = getStr();
                    Assert.NotNull(res);
                }
            );
        }

        private sealed class _FailingDynamicCellFormatter : DefaultTypeDescriber
        {
            private readonly int CellNum;
            private readonly int FailOn;

            public _FailingDynamicCellFormatter(int cellNum, int failOn)
            {
                CellNum = cellNum;
                FailOn = failOn;
            }

            public override int GetCellsForDynamicRow(in WriteContext ctx, dynamic row, Span<DynamicCellValue> cells)
            {
                if (cells.Length < CellNum)
                {
                    return CellNum;
                }

                for (var i = 0; i < CellNum; i++)
                {
                    var f =
                        i == FailOn ?
                            Formatter.ForDelegate((string value, in WriteContext context, IBufferWriter<char> buffer) => false) :
                            Formatter.ForDelegate((string value, in WriteContext context, IBufferWriter<char> buffer) => true);

                    cells[i] = DynamicCellValue.Create("Bar" + i, "foo" + i, f);
                }

                return CellNum;
            }
        }

        [Fact]
        public void FailingDynamicCellFormatter()
        {
            const int MAX_CELLS = 20;

            for (var i = 0; i < MAX_CELLS; i++)
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithTypeDescriber(new _FailingDynamicCellFormatter(MAX_CELLS, i)).ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        using (var w = getWriter())
                        using (var csv = config.CreateWriter(w))
                        {
                            Assert.Throws<SerializationException>(() => csv.Write(new object()));
                        }

                        getStr();
                    }
                );
            }
        }

        [Fact]
        public void LotsOfComments()
        {
            var opts = Options.CreateBuilder(Options.DynamicDefault).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

            RunSyncDynamicWriterVariants(
                opts,
                (config, getWriter, getStr) =>
                {
                    var cs = string.Join("\r\n", System.Linq.Enumerable.Repeat("foo", 1_000));

                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.WriteComment(cs);
                    }

                    var str = getStr();
                    var expected = string.Join("\r\n", System.Linq.Enumerable.Repeat("#foo", 1_000));
                    Assert.Equal(expected, str);
                }
            );
        }

        [Fact]
        public void WriteComment()
        {
            var dynOpts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();

            // no trailing new line
            {
                // first line, no headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("");
                            }

                            var res = getStr();
                            Assert.Equal("#", res);
                        }
                    );

                    // one line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello");
                            }

                            var res = getStr();
                            Assert.Equal("#hello", res);
                        }
                    );

                    // two lines
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello\r\nworld");
                            }

                            var res = getStr();
                            Assert.Equal("#hello\r\n#world", res);
                        }
                    );
                }

                // second line, no headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("");
                            }

                            var res = getStr();
                            Assert.Equal("#", res);
                        }
                    );

                    // one line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello");
                            }

                            var res = getStr();
                            Assert.Equal("123,456\r\n#hello", res);
                        }
                    );

                    // two lines
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello\r\nworld");
                            }

                            var res = getStr();
                            Assert.Equal("123,456\r\n#hello\r\n#world", res);
                        }
                    );
                }

                // second line, headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Always).ToOptions();

                    // one line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello", res);
                        }
                    );

                    // two lines
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello\r\nworld");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello\r\n#world", res);
                        }
                    );
                }

                // before row, no headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("");
                            }

                            var res = getStr();
                            Assert.Equal("#", res);
                        }
                    );

                    // one line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello");
                                csv.Write(new { Foo = 123, Bar = 456 });
                            }

                            var res = getStr();
                            Assert.Equal("#hello\r\n123,456", res);
                        }
                    );

                    // two lines
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello\r\nworld");
                                csv.Write(new { Foo = 123, Bar = 456 });
                            }

                            var res = getStr();
                            Assert.Equal("#hello\r\n#world\r\n123,456", res);
                        }
                    );
                }
            }

            // trailing new line
            {
                // first line, no headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("");
                            }

                            var res = getStr();
                            Assert.Equal("#\r\n", res);
                        }
                    );

                    // one line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello");
                            }

                            var res = getStr();
                            Assert.Equal("#hello\r\n", res);
                        }
                    );

                    // two lines
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello\r\nworld");
                            }

                            var res = getStr();
                            Assert.Equal("#hello\r\n#world\r\n", res);
                        }
                    );
                }

                // second line, no headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("");
                            }

                            var res = getStr();
                            Assert.Equal("#\r\n", res);
                        }
                    );

                    // one line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello");
                            }

                            var res = getStr();
                            Assert.Equal("123,456\r\n#hello\r\n", res);
                        }
                    );

                    // two lines
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello\r\nworld");
                            }

                            var res = getStr();
                            Assert.Equal("123,456\r\n#hello\r\n#world\r\n", res);
                        }
                    );
                }

                // second line, headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Always).ToOptions();

                    // one line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello\r\n", res);
                        }
                    );

                    // two lines
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello\r\nworld");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello\r\n#world\r\n", res);
                        }
                    );
                }

                // before row, no headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("");
                            }

                            var res = getStr();
                            Assert.Equal("#\r\n", res);
                        }
                    );

                    // one line
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello");
                                csv.Write(new { Foo = 123, Bar = 456 });
                            }

                            var res = getStr();
                            Assert.Equal("#hello\r\n123,456\r\n", res);
                        }
                    );

                    // two lines
                    RunSyncDynamicWriterVariants(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello\r\nworld");
                                csv.Write(new { Foo = 123, Bar = 456 });
                            }

                            var res = getStr();
                            Assert.Equal("#hello\r\n#world\r\n123,456\r\n", res);
                        }
                    );
                }
            }
        }

        [Fact]
        public void NeedEscapeColumnNames()
        {
            // \r\n
            {
                var opts =
                    Options.CreateBuilder(Options.Default)
                        .WithReadHeader(ReadHeader.Always)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithWriteRowEnding(WriteRowEnding.CarriageReturnLineFeed)
                        .ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo = MakeDynamicRow("\"He,llo\",\"\"\"\"\r\n123,456");

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo);
                        }

                        foo.Dispose();

                        var res = getStr();

                        Assert.Equal("\"He,llo\",\"\"\"\"\r\n123,456", res);
                    }
                );
            }
        }

        [Fact]
        public void Simple()
        {
            // \r\n
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithWriteRowEnding(WriteRowEnding.CarriageReturnLineFeed)
                        .ToOptions();

                // DynamicRow
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = MakeDynamicRow("Hello,World\r\n123,456");
                        dynamic foo2 = MakeDynamicRow("World,Hello\r\n12,789\r\n");

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                        }

                        foo1.Dispose();
                        foo2.Dispose();

                        var res = getStr();

                        Assert.Equal("Hello,World\r\n123,456\r\n789,12", res);
                    }
                );

                // ExpandoObject
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new ExpandoObject();
                        foo1.Hello = 123;
                        foo1.World = "456";
                        foo1.Fizz = null;
                        foo1.Fizz += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo2 = new ExpandoObject();
                        foo2.World = "12";
                        foo2.Hello = 789;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                        }

                        var res = getStr();

                        Assert.Equal("Hello,World\r\n123,456\r\n789,12", res);
                    }
                );

                // copy of ExpandoObject that we don't special case
                // so this is testing the raw case
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new FakeExpandoObject();
                        foo1.Hello = 123;
                        foo1.World = "456";
                        foo1.Fizz = null;
                        foo1.Fizz += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo2 = new FakeExpandoObject();
                        foo2.World = "12";
                        foo2.Hello = 789;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                        }

                        var res = getStr();

                        Assert.Equal("Hello,World\r\n123,456\r\n789,12", res);
                    }
                );

                // underlying is statically typed
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new { Hello = "123", World = 456 };
                        dynamic foo2 = new { Hello = "789", World = 012 };

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                        }

                        var res = getStr();

                        Assert.Equal("Hello,World\r\n123,456\r\n789,12", res);
                    }
                );

                // all of 'em mixed together
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new { Hello = "123", World = 456 };
                        dynamic foo2 = new FakeExpandoObject();
                        foo2.World = "789";
                        foo2.Hello = 111;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo3 = MakeDynamicRow("World,Hello\r\n222,333\r\n");
                        dynamic foo4 = new ExpandoObject();
                        foo4.World = "456";
                        foo4.Hello = 789;
                        foo4.Bar = null;
                        foo4.Bar += new EventHandler(delegate { throw new Exception(); });

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                            csv.Write(foo3);
                            csv.Write(foo4);
                        }

                        var res = getStr();

                        Assert.Equal("Hello,World\r\n123,456\r\n111,789\r\n333,222\r\n789,456", res);
                    }
                );
            }

            // \r
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithWriteRowEnding(WriteRowEnding.CarriageReturn)
                        .ToOptions();

                // DynamicRow
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = MakeDynamicRow("Hello,World\r\n123,456");
                        dynamic foo2 = MakeDynamicRow("World,Hello\r\n12,789\r\n");

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                        }

                        foo1.Dispose();
                        foo2.Dispose();

                        var res = getStr();

                        Assert.Equal("Hello,World\r123,456\r789,12", res);
                    }
                );

                // ExpandoObject
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new ExpandoObject();
                        foo1.Hello = 123;
                        foo1.World = "456";
                        foo1.Fizz = null;
                        foo1.Fizz += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo2 = new ExpandoObject();
                        foo2.World = "12";
                        foo2.Hello = 789;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                        }

                        var res = getStr();

                        Assert.Equal("Hello,World\r123,456\r789,12", res);
                    }
                );

                // copy of ExpandoObject that we don't special case
                // so this is testing the raw case
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new FakeExpandoObject();
                        foo1.Hello = 123;
                        foo1.World = "456";
                        foo1.Fizz = null;
                        foo1.Fizz += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo2 = new FakeExpandoObject();
                        foo2.World = "12";
                        foo2.Hello = 789;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                        }

                        var res = getStr();

                        Assert.Equal("Hello,World\r123,456\r789,12", res);
                    }
                );

                // underlying is statically typed
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new { Hello = "123", World = 456 };
                        dynamic foo2 = new { Hello = "789", World = 012 };

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                        }

                        var res = getStr();

                        Assert.Equal("Hello,World\r123,456\r789,12", res);
                    }
                );

                // all of 'em mixed together
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new { Hello = "123", World = 456 };
                        dynamic foo2 = new FakeExpandoObject();
                        foo2.World = "789";
                        foo2.Hello = 111;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo3 = MakeDynamicRow("World,Hello\r\n222,333\r\n");
                        dynamic foo4 = new ExpandoObject();
                        foo4.World = "456";
                        foo4.Hello = 789;
                        foo4.Bar = null;
                        foo4.Bar += new EventHandler(delegate { throw new Exception(); });

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                            csv.Write(foo3);
                            csv.Write(foo4);
                        }

                        var res = getStr();

                        Assert.Equal("Hello,World\r123,456\r111,789\r333,222\r789,456", res);
                    }
                );
            }

            // \n
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithWriteRowEnding(WriteRowEnding.LineFeed)
                        .ToOptions();

                // DynamicRow
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = MakeDynamicRow("Hello,World\r\n123,456");
                        dynamic foo2 = MakeDynamicRow("World,Hello\r\n12,789\r\n");

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                        }

                        foo1.Dispose();
                        foo2.Dispose();

                        var res = getStr();

                        Assert.Equal("Hello,World\n123,456\n789,12", res);
                    }
                );

                // ExpandoObject
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new ExpandoObject();
                        foo1.Hello = 123;
                        foo1.World = "456";
                        foo1.Fizz = null;
                        foo1.Fizz += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo2 = new ExpandoObject();
                        foo2.World = "12";
                        foo2.Hello = 789;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                        }

                        var res = getStr();

                        Assert.Equal("Hello,World\n123,456\n789,12", res);
                    }
                );

                // copy of ExpandoObject that we don't special case
                // so this is testing the raw case
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new FakeExpandoObject();
                        foo1.Hello = 123;
                        foo1.World = "456";
                        foo1.Fizz = null;
                        foo1.Fizz += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo2 = new FakeExpandoObject();
                        foo2.World = "12";
                        foo2.Hello = 789;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                        }

                        var res = getStr();

                        Assert.Equal("Hello,World\n123,456\n789,12", res);
                    }
                );

                // underlying is statically typed
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new { Hello = "123", World = 456 };
                        dynamic foo2 = new { Hello = "789", World = 012 };

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                        }

                        var res = getStr();

                        Assert.Equal("Hello,World\n123,456\n789,12", res);
                    }
                );

                // all of 'em mixed together
                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new { Hello = "123", World = 456 };
                        dynamic foo2 = new FakeExpandoObject();
                        foo2.World = "789";
                        foo2.Hello = 111;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo3 = MakeDynamicRow("World,Hello\r\n222,333\r\n");
                        dynamic foo4 = new ExpandoObject();
                        foo4.World = "456";
                        foo4.Hello = 789;
                        foo4.Bar = null;
                        foo4.Bar += new EventHandler(delegate { throw new Exception(); });

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(foo1);
                            csv.Write(foo2);
                            csv.Write(foo3);
                            csv.Write(foo4);
                        }

                        var res = getStr();

                        Assert.Equal("Hello,World\n123,456\n111,789\n333,222\n789,456", res);
                    }
                );
            }
        }

        [Fact]
        public void CommentEscape()
        {
            // \r\n
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Never)
                        .WithWriteRowEnding(WriteRowEnding.CarriageReturnLineFeed)
                        .WithCommentCharacter('#')
                        .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always)
                        .ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(MakeDynamicRow("Hello,World\r\n#hello,foo"));
                        }

                        var txt = getString();
                        Assert.Equal("\"#hello\",foo\r\n", txt);
                    }
                );

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(MakeDynamicRow("Hello,World\r\nhello,fo#o"));
                        }

                        var txt = getString();
                        Assert.Equal("hello,\"fo#o\"\r\n", txt);
                    }
                );
            }

            // \r
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Never)
                        .WithWriteRowEnding(WriteRowEnding.CarriageReturn)
                        .WithCommentCharacter('#')
                        .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always)
                        .ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(MakeDynamicRow("Hello,World\r\n#hello,foo"));
                        }

                        var txt = getString();
                        Assert.Equal("\"#hello\",foo\r", txt);
                    }
                );

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(MakeDynamicRow("Hello,World\r\nhello,fo#o"));
                        }

                        var txt = getString();
                        Assert.Equal("hello,\"fo#o\"\r", txt);
                    }
                );
            }

            // \n
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Never)
                        .WithWriteRowEnding(WriteRowEnding.LineFeed)
                        .WithCommentCharacter('#')
                        .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always)
                        .ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(MakeDynamicRow("Hello,World\r\n#hello,foo"));
                        }

                        var txt = getString();
                        Assert.Equal("\"#hello\",foo\n", txt);
                    }
                );

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(MakeDynamicRow("Hello,World\r\nhello,fo#o"));
                        }

                        var txt = getString();
                        Assert.Equal("hello,\"fo#o\"\n", txt);
                    }
                );
            }
        }

        [Fact]
        public void EscapeHeaders()
        {
            // \r\n
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithWriteRowEnding(WriteRowEnding.CarriageReturnLineFeed)
                        .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always)
                        .ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            IDictionary<string, object> expando1 = new ExpandoObject();
                            expando1["hello\r\nworld"] = "fizz";
                            expando1["foo,bar"] = "buzz";
                            expando1["yup"] = "yes";

                            IDictionary<string, object> expando2 = new ExpandoObject();
                            expando2["hello\r\nworld"] = "ping";
                            expando2["foo,bar"] = "pong";
                            expando2["yup"] = "no";

                            writer.Write(expando1);
                            writer.Write(expando2);
                        }

                        var txt = getString();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\r\nfizz,buzz,yes\r\nping,pong,no\r\n", txt);
                    }
                );
            }

            // \r
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithWriteRowEnding(WriteRowEnding.CarriageReturn)
                        .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always)
                        .ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            IDictionary<string, object> expando1 = new ExpandoObject();
                            expando1["hello\r\nworld"] = "fizz";
                            expando1["foo,bar"] = "buzz";
                            expando1["yup"] = "yes";

                            IDictionary<string, object> expando2 = new ExpandoObject();
                            expando2["hello\r\nworld"] = "ping";
                            expando2["foo,bar"] = "pong";
                            expando2["yup"] = "no";

                            writer.Write(expando1);
                            writer.Write(expando2);
                        }

                        var txt = getString();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\rfizz,buzz,yes\rping,pong,no\r", txt);
                    }
                );
            }

            // \n
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithWriteRowEnding(WriteRowEnding.LineFeed)
                        .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always)
                        .ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            IDictionary<string, object> expando1 = new ExpandoObject();
                            expando1["hello\r\nworld"] = "fizz";
                            expando1["foo,bar"] = "buzz";
                            expando1["yup"] = "yes";

                            IDictionary<string, object> expando2 = new ExpandoObject();
                            expando2["hello\r\nworld"] = "ping";
                            expando2["foo,bar"] = "pong";
                            expando2["yup"] = "no";

                            writer.Write(expando1);
                            writer.Write(expando2);
                        }

                        var txt = getString();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\nfizz,buzz,yes\nping,pong,no\n", txt);
                    }
                );
            }
        }

        [Fact]
        public void NeedEscape()
        {
            // \r\n
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Never).WithWriteRowEnding(WriteRowEnding.CarriageReturnLineFeed).ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new { Foo = "hello,world", Bar = 123, Nope = 456 });
                            writer.Write(new { Foo = "foo\"bar", Bar = 789, Nope = default(string) });
                            writer.Write(new { Foo = "fizz\r\nbuzz", Bar = -12, Nope = 34 });
                        }

                        var txt = getString();
                        Assert.Equal("\"hello,world\",123,456\r\n\"foo\"\"bar\",789,\r\n\"fizz\r\nbuzz\",-12,34", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Never).WithWriteRowEnding(WriteRowEnding.CarriageReturn).ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new { Foo = "hello,world", Bar = 123, Nope = 456 });
                            writer.Write(new { Foo = "foo\"bar", Bar = 789, Nope = default(string) });
                            writer.Write(new { Foo = "fizz\r\nbuzz", Bar = -12, Nope = 34 });
                        }

                        var txt = getString();
                        Assert.Equal("\"hello,world\",123,456\r\"foo\"\"bar\",789,\r\"fizz\r\nbuzz\",-12,34", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Never).WithWriteRowEnding(WriteRowEnding.LineFeed).ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new { Foo = "hello,world", Bar = 123, Nope = 456 });
                            writer.Write(new { Foo = "foo\"bar", Bar = 789, Nope = default(string) });
                            writer.Write(new { Foo = "fizz\r\nbuzz", Bar = -12, Nope = 34 });
                        }

                        var txt = getString();
                        Assert.Equal("\"hello,world\",123,456\n\"foo\"\"bar\",789,\n\"fizz\r\nbuzz\",-12,34", txt);
                    }
                );
            }

            // large
            {
                var opts = Options.DynamicDefault;
                var val = string.Join("", System.Linq.Enumerable.Repeat("abc\r\n", 450));

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new { Foo = val, Bar = 001, Nope = 009 });
                        }

                        var txt = getString();
                        Assert.Equal("Foo,Bar,Nope\r\n\"abc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\n\",1,9", txt);
                    }
                );
            }
        }

        [Fact]
        public void WriteAll()
        {
            // \r\n
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Always).WithWriteRowEnding(WriteRowEnding.CarriageReturnLineFeed).ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.WriteAll(
                                new object[]
                                {
                                    new { Foo = "hello", Bar = 123, Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Buzz = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero) },
                                    new { Bar = 456, Buzz = new DateTimeOffset(1980, 2, 2, 1, 1, 1, TimeSpan.Zero), Fizz = default(Guid?), Foo = "hello" },
                                    new { Bar = 789, Buzz = new DateTimeOffset(1990, 3, 3, 2, 2, 2, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = default(string) },
                                    new { Bar = 012, Buzz = new DateTimeOffset(2000, 4, 4, 3, 3, 3, TimeSpan.Zero), Fizz = default(Guid?), Foo = default(string) }
                                }
                            );
                        }

                        var txt = getString();
                        Assert.Equal("Foo,Bar,Fizz,Buzz\r\nhello,123,5dc798f5-6477-4216-8567-9d17c05fa87e,1970-01-01 00:00:00Z\r\nhello,456,,1980-02-02 01:01:01Z\r\n,789,5dc798f5-6477-4216-8567-9d17c05fa87e,1990-03-03 02:02:02Z\r\n,12,,2000-04-04 03:03:03Z", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Always).WithWriteRowEnding(WriteRowEnding.CarriageReturn).ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.WriteAll(
                                new object[]
                                {
                                    new { Foo = "hello", Bar = 123, Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Buzz = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero) },
                                    new { Bar = 456, Buzz = new DateTimeOffset(1980, 2, 2, 1, 1, 1, TimeSpan.Zero), Fizz = default(Guid?), Foo = "hello" },
                                    new { Bar = 789, Buzz = new DateTimeOffset(1990, 3, 3, 2, 2, 2, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = default(string) },
                                    new { Bar = 012, Buzz = new DateTimeOffset(2000, 4, 4, 3, 3, 3, TimeSpan.Zero), Fizz = default(Guid?), Foo = default(string) }
                                }
                            );
                        }

                        var txt = getString();
                        Assert.Equal("Foo,Bar,Fizz,Buzz\rhello,123,5dc798f5-6477-4216-8567-9d17c05fa87e,1970-01-01 00:00:00Z\rhello,456,,1980-02-02 01:01:01Z\r,789,5dc798f5-6477-4216-8567-9d17c05fa87e,1990-03-03 02:02:02Z\r,12,,2000-04-04 03:03:03Z", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Always).WithWriteRowEnding(WriteRowEnding.LineFeed).ToOptions();

                RunSyncDynamicWriterVariants(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.WriteAll(
                                new object[]
                                {
                                    new { Foo = "hello", Bar = 123, Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Buzz = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero) },
                                    new { Bar = 456, Buzz = new DateTimeOffset(1980, 2, 2, 1, 1, 1, TimeSpan.Zero), Fizz = default(Guid?), Foo = "hello" },
                                    new { Bar = 789, Buzz = new DateTimeOffset(1990, 3, 3, 2, 2, 2, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = default(string) },
                                    new { Bar = 012, Buzz = new DateTimeOffset(2000, 4, 4, 3, 3, 3, TimeSpan.Zero), Fizz = default(Guid?), Foo = default(string) }
                                }
                            );
                        }

                        var txt = getString();
                        Assert.Equal("Foo,Bar,Fizz,Buzz\nhello,123,5dc798f5-6477-4216-8567-9d17c05fa87e,1970-01-01 00:00:00Z\nhello,456,,1980-02-02 01:01:01Z\n,789,5dc798f5-6477-4216-8567-9d17c05fa87e,1990-03-03 02:02:02Z\n,12,,2000-04-04 03:03:03Z", txt);
                    }
                );
            }
        }

        // async tests

        [Fact]
        public async Task WriteRangeAsync()
        {
            var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Never).ToOptions();

            await RunAsyncDynamicWriterVariants(
                opts,
                async (config, getWriter, getStr) =>
                {
                    var row = MakeDynamicRow($"A,B,C,D\r\n1,2,3,4");
                    var range1 = row[1..^1];
                    var range2 = row[1..];

                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteAsync(range1);
                        await csv.WriteAsync(range2);
                    }

                    var res = await getStr();
                    Assert.Equal("2,3\r\n2,3,4", res);

                    row.Dispose();
                    range1.Dispose();
                    range2.Dispose();
                }
            );
        }

        [Fact]
        public async Task WriteCommentErrorsAsync()
        {
            await RunAsyncDynamicWriterVariants(
                Options.DynamicDefault,
                async (config, getWriter, getStr) =>
                {
                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        var exc = await Assert.ThrowsAsync<InvalidOperationException>(async () => await csv.WriteCommentAsync("foo"));
                        Assert.Equal($"No {nameof(Options.CommentCharacter)} configured, cannot write a comment line", exc.Message);
                    }

                    await getStr();
                }
            );
        }

        [Fact]
        public async Task IllegalRowSizesAsync()
        {
            await RunAsyncDynamicWriterVariants(
                Options.DynamicDefault,         // this only happens if you're writing headers, which we do by default
                async (config, getWriter, getStr) =>
                {
                    var row = MakeDynamicRow("A,B,C\r\n1,2,3");
                    var tooBigRow = MakeDynamicRow("A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q,R,S,T,U,V\r\n4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25");

                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteAsync(row);

                        var exc = await Assert.ThrowsAsync<InvalidOperationException>(async () => { await csv.WriteAsync(tooBigRow); });
                        Assert.Equal("Too many cells returned, could not place in desired order", exc.Message);
                    }

                    await getStr();
                }
            );
        }

        [Fact]
        public async Task VariableSizeRowsAsync()
        {
            var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Never).ToOptions();

            // smaller first
            await RunAsyncDynamicWriterVariants(
                opts,
                async (config, getWriter, getStr) =>
                {
                    var r1 = MakeDynamicRow("A,B,C\r\n1,2,3");
                    var r2 = MakeDynamicRow("A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q,R,S,T,U,V\r\n4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25");

                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteAsync(r1);
                        await csv.WriteAsync(r2);
                    }

                    var res = await getStr();
                    Assert.Equal("1,2,3\r\n4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25", res);
                }
            );

            // larger first
            await RunAsyncDynamicWriterVariants(
                opts,
                async (config, getWriter, getStr) =>
                {
                    var r1 = MakeDynamicRow("A,B,C\r\n1,2,3");
                    var r2 = MakeDynamicRow("A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q,R,S,T,U,V\r\n4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25");

                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteAsync(r2);
                        await csv.WriteAsync(r1);
                    }

                    var res = await getStr();
                    Assert.Equal("4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25\r\n1,2,3", res);
                }
            );
        }

        [Fact]
        public async Task ReturnedRowCountsAsync()
        {
            // simple
            await RunAsyncDynamicWriterVariants(
                Options.DynamicDefault,
                async (config, getWriter, getStr) =>
                {
                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        var n1 =
                            await csv.WriteAllAsync(
                                new[]
                                {
                                    MakeDynamicRow("A,B\r\nFoo,123"),
                                    MakeDynamicRow("A,B\r\nBar,456"),
                                    MakeDynamicRow("A,B\r\nFizz,456")
                                }
                            );

                        Assert.Equal(3, n1);

                        var n2 =
                            await csv.WriteAllAsync(
                                new[]
                                {
                                    MakeDynamicRow("A,B\r\nBuzz,789")
                                }
                            );

                        Assert.Equal(1, n2);
                    }

                    var res = await getStr();
                    Assert.Equal("A,B\r\nFoo,123\r\nBar,456\r\nFizz,456\r\nBuzz,789", res);
                }
            );

            // no count
            await RunAsyncDynamicWriterVariants(
                Options.DynamicDefault,
                async (config, getWriter, getStr) =>
                {
                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        var n1 =
                            await csv.WriteAllAsync(
                                Enumerable.Range(0, 10).Select(x => MakeDynamicRow($"Item1,Item2\r\n{x},{x * 2}"))
                            );

                        Assert.Equal(10, n1);

                        var n2 =
                            await csv.WriteAllAsync(
                                new[] { MakeDynamicRow($"Item1,Item2\r\nnope,-1") }
                            );
                    }

                    var res = await getStr();
                    Assert.Equal("Item1,Item2\r\n0,0\r\n1,2\r\n2,4\r\n3,6\r\n4,8\r\n5,10\r\n6,12\r\n7,14\r\n8,16\r\n9,18\r\nnope,-1", res);
                }
            );

            // no count, async
            await RunAsyncDynamicWriterVariants(
                Options.DynamicDefault,
                async (config, getWriter, getStr) =>
                {
                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        var enumerable1 = new TestAsyncEnumerable<dynamic>(Enumerable.Range(0, 10).Select(x => MakeDynamicRow($"Item1,Item2\r\n{x + "-" + x},{x * 2}")), false);

                        var n1 = await csv.WriteAllAsync(enumerable1);
                        Assert.Equal(10, n1);

                        var enumerable2 = new TestAsyncEnumerable<dynamic>(new[] { MakeDynamicRow($"Item1,Item2\r\nhello,fifteen") }, false);

                        var n2 = await csv.WriteAllAsync(enumerable2);
                        Assert.Equal(1, n2);
                    }

                    var res = await getStr();
                    Assert.Equal("Item1,Item2\r\n0-0,0\r\n1-1,2\r\n2-2,4\r\n3-3,6\r\n4-4,8\r\n5-5,10\r\n6-6,12\r\n7-7,14\r\n8-8,16\r\n9-9,18\r\nhello,fifteen", res);
                }
            );
        }

        [Fact]
        public async Task MultiCharacterValueSeparatorsAsync()
        {
            var opts = Options.CreateBuilder(Options.DynamicDefault).WithValueSeparator("#*#").ToOptions();

            // no escapes
            {
                var r1 = MakeDynamicRow("A,B\r\n123,foo");
                var r2 = MakeDynamicRow("A,B\r\n456,#");
                var r3 = MakeDynamicRow("A,B\r\n789,*");

                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(r1);
                            await csv.WriteAsync(r2);
                            await csv.WriteAsync(r3);
                        }

                        var res = await getStr();
                        Assert.Equal("A#*#B\r\n123#*#foo\r\n456#*##\r\n789#*#*", res);
                    }
                );
            }

            // escapes
            {
                var r1 = MakeDynamicRow("A,B\r\n123,foo#*#bar");
                var r2 = MakeDynamicRow("A,B\r\n456,#");
                var r3 = MakeDynamicRow("A,B\r\n789,*");

                await RunAsyncDynamicWriterVariants(
                   opts,
                   async (config, getWriter, getStr) =>
                   {
                       await using (var writer = getWriter())
                       await using (var csv = config.CreateAsyncWriter(writer))
                       {
                           await csv.WriteAsync(r1);
                           await csv.WriteAsync(r2);
                           await csv.WriteAsync(r3);
                       }

                       var res = await getStr();
                       Assert.Equal("A#*#B\r\n123#*#\"foo#*#bar\"\r\n456#*##\r\n789#*#*", res);
                   }
               );
            }

            // in headers
            {
                var r1 = MakeDynamicRow("A#*#Escaped,B\r\n123,foo#*#bar");
                var r2 = MakeDynamicRow("A#*#Escaped,B\r\n456,#");
                var r3 = MakeDynamicRow("A#*#Escaped,B\r\n789,*");

                await RunAsyncDynamicWriterVariants(
                  opts,
                  async (config, getWriter, getStr) =>
                  {
                      await using (var writer = getWriter())
                      await using (var csv = config.CreateAsyncWriter(writer))
                      {
                          await csv.WriteAsync(r1);
                          await csv.WriteAsync(r2);
                          await csv.WriteAsync(r3);
                      }

                      var res = await getStr();
                      Assert.Equal("\"A#*#Escaped\"#*#B\r\n123#*#\"foo#*#bar\"\r\n456#*##\r\n789#*#*", res);
                  }
              );
            }
        }

        [Fact]
        public async Task WriteCommentBeforeRowAsync()
        {
            // no headers
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Never).WithCommentCharacter('#').ToOptions();
                // comment, then row
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteCommentAsync("hello world");
                            await csv.WriteAsync(new { Foo = 123, Bar = "+456" });
                        }

                        var res = await getStr();
                        Assert.Equal("#hello world\r\n123,+456", res);
                    }
                );

                // comment, no row
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteCommentAsync("hello world");
                        }

                        var res = await getStr();
                        Assert.Equal("#hello world", res);
                    }
                );

                // multiple comments, then row
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteCommentAsync("hello world");
                            await csv.WriteCommentAsync("fizz buzz");
                            await csv.WriteCommentAsync("foo\r\nbar");
                            await csv.WriteAsync(new { Foo = 123, Bar = "+456" });
                        }

                        var res = await getStr();
                        Assert.Equal("#hello world\r\n#fizz buzz\r\n#foo\r\n#bar\r\n123,+456", res);
                    }
                );

                // multiple comments, no row
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteCommentAsync("hello world");
                            await csv.WriteCommentAsync("fizz buzz");
                            await csv.WriteCommentAsync("foo\r\nbar");
                        }

                        var res = await getStr();
                        Assert.Equal("#hello world\r\n#fizz buzz\r\n#foo\r\n#bar", res);
                    }
                );
            }

            // headers
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithCommentCharacter('#').ToOptions();
                // comment, then row
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteCommentAsync("hello world");
                            await csv.WriteAsync(new { Foo = 123, Bar = "+456" });
                        }

                        var res = await getStr();
                        Assert.Equal("#hello world\r\nFoo,Bar\r\n123,+456", res);
                    }
                );

                // comment, no row
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteCommentAsync("hello world");
                        }

                        var res = await getStr();
                        Assert.Equal("#hello world", res);
                    }
                );

                // multiple comments, then row
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteCommentAsync("hello world");
                            await csv.WriteCommentAsync("fizz buzz");
                            await csv.WriteCommentAsync("foo\r\nbar");
                            await csv.WriteAsync(new { Foo = 123, Bar = "+456" });
                        }

                        var res = await getStr();
                        Assert.Equal("#hello world\r\n#fizz buzz\r\n#foo\r\n#bar\r\nFoo,Bar\r\n123,+456", res);
                    }
                );

                // multiple comments, no row
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteCommentAsync("hello world");
                            await csv.WriteCommentAsync("fizz buzz");
                            await csv.WriteCommentAsync("foo\r\nbar");
                        }

                        var res = await getStr();
                        Assert.Equal("#hello world\r\n#fizz buzz\r\n#foo\r\n#bar", res);
                    }
                );
            }
        }

        [Fact]
        public async Task ChainedFormattersAsync()
        {
            var f1 =
                Formatter.ForDelegate<string>(
                    (string data, in WriteContext ctx, IBufferWriter<char> writer) =>
                    {
                        var num = ((_ChainedFormatters_Context)ctx.Context).F;

                        if (num != 1) return false;

                        var span = writer.GetSpan(4);
                        span[0] = '1';
                        span[1] = '2';
                        span[2] = '3';
                        span[3] = '4';

                        writer.Advance(4);

                        return true;
                    }
                );
            var f2 =
                Formatter.ForDelegate<string>(
                    (string data, in WriteContext ctx, IBufferWriter<char> writer) =>
                    {
                        var num = ((_ChainedFormatters_Context)ctx.Context).F;

                        if (num != 2) return false;

                        var span = writer.GetSpan(3);
                        span[0] = 'a';
                        span[1] = 'b';
                        span[2] = 'c';

                        writer.Advance(3);

                        return true;
                    }
                );
            var f3 =
                Formatter.ForDelegate<string>(
                    (string data, in WriteContext ctx, IBufferWriter<char> writer) =>
                    {
                        var num = ((_ChainedFormatters_Context)ctx.Context).F;

                        if (num != 3) return false;

                        var span = writer.GetSpan(2);
                        span[0] = '0';
                        span[1] = '0';

                        writer.Advance(2);

                        return true;
                    }
                );

            var f = f1.Else(f2).Else(f3);

            var td = new _ChainedFormatters_TypeDescriber(f);

            var opts = OptionsBuilder.CreateBuilder(Options.DynamicDefault).WithTypeDescriber(td).ToOptions();

            var row = MakeDynamicRow("Foo\r\nabc");
            try
            {
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        var ctx = new _ChainedFormatters_Context();

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer, ctx))
                        {
                            ctx.F = 1;
                            await csv.WriteAsync(row);
                            ctx.F = 2;
                            await csv.WriteAsync(row);
                            ctx.F = 3;
                            await csv.WriteAsync(row);
                            ctx.F = 1;
                            await csv.WriteAsync(row);
                        }

                        var str = await getStr();
                        Assert.Equal("Foo\r\n1234\r\nabc\r\n00\r\n1234", str);
                    }
                );
            }
            finally
            {
                row.Dispose();
            }
        }

        [Fact]
        public async Task NoEscapesAsync()
        {
            // no escapes at all (TSV)
            {
                var opts = OptionsBuilder.CreateBuilder(Options.DynamicDefault).WithValueSeparator("\t").WithEscapedValueStartAndEnd(null).WithEscapedValueEscapeCharacter(null).ToOptions();

                // correct
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(new { Foo = "abc", Bar = "123" });
                            await csv.WriteAsync(new { Foo = "\"", Bar = "," });
                        }

                        var str = await getStr();
                        Assert.Equal("Foo\tBar\r\nabc\t123\r\n\"\t,", str);
                    }
                );

                // explodes if there's an value separator in a value, since there are no escapes
                {
                    // \t
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                var inv = await Assert.ThrowsAsync<InvalidOperationException>(async () => await csv.WriteAsync(new { Foo = "\t", Bar = "foo" }));

                                Assert.Equal("Tried to write a value contain '\t' which requires escaping a value, but no way to escape a value is configured", inv.Message);
                            }

                            await getStr();
                        }
                    );

                    // \r\n
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                var inv = await Assert.ThrowsAsync<InvalidOperationException>(async () => await csv.WriteAsync(new { Foo = "foo", Bar = "\r\n" }));

                                Assert.Equal("Tried to write a value contain '\r' which requires escaping a value, but no way to escape a value is configured", inv.Message);
                            }

                            await getStr();
                        }
                    );

                    var optsWithComment = OptionsBuilder.CreateBuilder(opts).WithCommentCharacter('#').ToOptions();
                    // #
                    await RunAsyncDynamicWriterVariants(
                        optsWithComment,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                var inv = await Assert.ThrowsAsync<InvalidOperationException>(async () => await csv.WriteAsync(new { Foo = "#", Bar = "fizz" }));

                                Assert.Equal("Tried to write a value contain '#' which requires escaping a value, but no way to escape a value is configured", inv.Message);
                            }

                            await getStr();
                        }
                    );
                }
            }

            // escapes, but no escape for the escape start and end char
            {
                var opts = OptionsBuilder.CreateBuilder(Options.DynamicDefault).WithValueSeparator("\t").WithEscapedValueStartAndEnd('"').WithEscapedValueEscapeCharacter(null).ToOptions();

                // correct
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(new { Foo = "a\tbc", Bar = "#123" });
                            await csv.WriteAsync(new { Foo = "\r", Bar = "," });
                        }

                        var str = await getStr();
                        Assert.Equal("Foo\tBar\r\n\"a\tbc\"\t#123\r\n\"\r\"\t,", str);
                    }
                );

                var optsWithComments = OptionsBuilder.CreateBuilder(opts).WithCommentCharacter('#').ToOptions();

                // correct with comments
                await RunAsyncDynamicWriterVariants(
                    optsWithComments,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(new { Foo = "a\tbc", Bar = "#123" });
                            await csv.WriteAsync(new { Foo = "\r", Bar = "," });
                        }

                        var str = await getStr();
                        Assert.Equal("Foo\tBar\r\n\"a\tbc\"\t\"#123\"\r\n\"\r\"\t,", str);
                    }
                );

                // explodes if there's an escape start character in a value, since it can't be escaped
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            var inv = await Assert.ThrowsAsync<InvalidOperationException>(async () => await csv.WriteAsync(new { Foo = "a\tbc", Bar = "\"" }));

                            Assert.Equal("Tried to write a value contain '\"' which requires escaping the character in an escaped value, but no way to escape inside an escaped value is configured", inv.Message);
                        }

                        await getStr();
                    }
                );
            }
        }

        [Fact]
        public async Task NullCommentAsync()
        {
            await RunAsyncDynamicWriterVariants(
                Options.DynamicDefault,
                async (config, getWriter, getStr) =>
                {
                    await using (var w = getWriter())
                    await using (var csv = config.CreateAsyncWriter(w))
                    {
                        await Assert.ThrowsAsync<ArgumentNullException>(async () => await csv.WriteCommentAsync(default(string)));
                    }

                    var res = await getStr();
                    Assert.NotNull(res);
                }
            );
        }

        [Fact]
        public async Task FailingDynamicCellFormatterAsync()
        {
            const int MAX_CELLS = 20;
            for (var i = 0; i < MAX_CELLS; i++)
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithTypeDescriber(new _FailingDynamicCellFormatter(MAX_CELLS, i)).ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var w = getWriter())
                        await using (var csv = config.CreateAsyncWriter(w))
                        {
                            await Assert.ThrowsAsync<SerializationException>(async () => await csv.WriteAsync(new object()));
                        }

                        await getStr();
                    }
                );
            }
        }

        [Fact]
        public async Task LotsOfCommentsAsync()
        {
            var opts = Options.CreateBuilder(Options.DynamicDefault).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

            await RunAsyncDynamicWriterVariants(
                opts,
                async (config, getWriter, getStr) =>
                {
                    var cs = string.Join("\r\n", System.Linq.Enumerable.Repeat("foo", 1_000));

                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteCommentAsync(cs);
                    }

                    var str = await getStr();
                    var expected = string.Join("\r\n", System.Linq.Enumerable.Repeat("#foo", 1_000));
                    Assert.Equal(expected, str);
                }
            );
        }

        [Fact]
        public async Task SimpleAsync()
        {
            // \r\n
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithWriteRowEnding(WriteRowEnding.CarriageReturnLineFeed)
                        .ToOptions();

                // DynamicRow
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = MakeDynamicRow("Hello,World\r\n123,456");
                        dynamic foo2 = MakeDynamicRow("World,Hello\r\n12,789\r\n");

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                        }

                        foo1.Dispose();
                        foo2.Dispose();

                        var res = await getStr();

                        Assert.Equal("Hello,World\r\n123,456\r\n789,12", res);
                    }
                );

                // ExpandoObject
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new ExpandoObject();
                        foo1.Hello = 123;
                        foo1.World = "456";
                        foo1.Fizz = null;
                        foo1.Fizz += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo2 = new ExpandoObject();
                        foo2.World = "12";
                        foo2.Hello = 789;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                        }

                        var res = await getStr();

                        Assert.Equal("Hello,World\r\n123,456\r\n789,12", res);
                    }
                );

                // copy of ExpandoObject that we don't special case
                // so this is testing the raw case
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new FakeExpandoObject();
                        foo1.Hello = 123;
                        foo1.World = "456";
                        foo1.Fizz = null;
                        foo1.Fizz += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo2 = new FakeExpandoObject();
                        foo2.World = "12";
                        foo2.Hello = 789;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                        }

                        var res = await getStr();

                        Assert.Equal("Hello,World\r\n123,456\r\n789,12", res);
                    }
                );

                // underlying is statically typed
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new { Hello = "123", World = 456 };
                        dynamic foo2 = new { Hello = "789", World = 012 };

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                        }

                        var res = await getStr();

                        Assert.Equal("Hello,World\r\n123,456\r\n789,12", res);
                    }
                );

                // all of 'em mixed together
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new { Hello = "123", World = 456 };
                        dynamic foo2 = new FakeExpandoObject();
                        foo2.World = "789";
                        foo2.Hello = 111;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo3 = MakeDynamicRow("World,Hello\r\n222,333\r\n");
                        dynamic foo4 = new ExpandoObject();
                        foo4.World = "456";
                        foo4.Hello = 789;
                        foo4.Bar = null;
                        foo4.Bar += new EventHandler(delegate { throw new Exception(); });

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                            await csv.WriteAsync(foo3);
                            await csv.WriteAsync(foo4);
                        }

                        var res = await getStr();

                        Assert.Equal("Hello,World\r\n123,456\r\n111,789\r\n333,222\r\n789,456", res);
                    }
                );
            }

            // \r
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithWriteRowEnding(WriteRowEnding.CarriageReturn)
                        .ToOptions();

                // DynamicRow
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = MakeDynamicRow("Hello,World\r\n123,456");
                        dynamic foo2 = MakeDynamicRow("World,Hello\r\n12,789\r\n");

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                        }

                        foo1.Dispose();
                        foo2.Dispose();

                        var res = await getStr();

                        Assert.Equal("Hello,World\r123,456\r789,12", res);
                    }
                );

                // ExpandoObject
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new ExpandoObject();
                        foo1.Hello = 123;
                        foo1.World = "456";
                        foo1.Fizz = null;
                        foo1.Fizz += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo2 = new ExpandoObject();
                        foo2.World = "12";
                        foo2.Hello = 789;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                        }

                        var res = await getStr();

                        Assert.Equal("Hello,World\r123,456\r789,12", res);
                    }
                );

                // copy of ExpandoObject that we don't special case
                // so this is testing the raw case
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new FakeExpandoObject();
                        foo1.Hello = 123;
                        foo1.World = "456";
                        foo1.Fizz = null;
                        foo1.Fizz += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo2 = new FakeExpandoObject();
                        foo2.World = "12";
                        foo2.Hello = 789;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                        }

                        var res = await getStr();

                        Assert.Equal("Hello,World\r123,456\r789,12", res);
                    }
                );

                // underlying is statically typed
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new { Hello = "123", World = 456 };
                        dynamic foo2 = new { Hello = "789", World = 012 };

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                        }

                        var res = await getStr();

                        Assert.Equal("Hello,World\r123,456\r789,12", res);
                    }
                );

                // all of 'em mixed together
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new { Hello = "123", World = 456 };
                        dynamic foo2 = new FakeExpandoObject();
                        foo2.World = "789";
                        foo2.Hello = 111;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo3 = MakeDynamicRow("World,Hello\r\n222,333\r\n");
                        dynamic foo4 = new ExpandoObject();
                        foo4.World = "456";
                        foo4.Hello = 789;
                        foo4.Bar = null;
                        foo4.Bar += new EventHandler(delegate { throw new Exception(); });

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                            await csv.WriteAsync(foo3);
                            await csv.WriteAsync(foo4);
                        }

                        var res = await getStr();

                        Assert.Equal("Hello,World\r123,456\r111,789\r333,222\r789,456", res);
                    }
                );
            }

            // \n
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithWriteRowEnding(WriteRowEnding.LineFeed)
                        .ToOptions();

                // DynamicRow
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = MakeDynamicRow("Hello,World\r\n123,456");
                        dynamic foo2 = MakeDynamicRow("World,Hello\r\n12,789\r\n");

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                        }

                        foo1.Dispose();
                        foo2.Dispose();

                        var res = await getStr();

                        Assert.Equal("Hello,World\n123,456\n789,12", res);
                    }
                );

                // ExpandoObject
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new ExpandoObject();
                        foo1.Hello = 123;
                        foo1.World = "456";
                        foo1.Fizz = null;
                        foo1.Fizz += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo2 = new ExpandoObject();
                        foo2.World = "12";
                        foo2.Hello = 789;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                        }

                        var res = await getStr();

                        Assert.Equal("Hello,World\n123,456\n789,12", res);
                    }
                );

                // copy of ExpandoObject that we don't special case
                // so this is testing the raw case
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new FakeExpandoObject();
                        foo1.Hello = 123;
                        foo1.World = "456";
                        foo1.Fizz = null;
                        foo1.Fizz += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo2 = new FakeExpandoObject();
                        foo2.World = "12";
                        foo2.Hello = 789;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                        }

                        var res = await getStr();

                        Assert.Equal("Hello,World\n123,456\n789,12", res);
                    }
                );

                // underlying is statically typed
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new { Hello = "123", World = 456 };
                        dynamic foo2 = new { Hello = "789", World = 012 };

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                        }

                        var res = await getStr();

                        Assert.Equal("Hello,World\n123,456\n789,12", res);
                    }
                );

                // all of 'em mixed together
                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo1 = new { Hello = "123", World = 456 };
                        dynamic foo2 = new FakeExpandoObject();
                        foo2.World = "789";
                        foo2.Hello = 111;
                        foo2.Bar = null;
                        foo2.Bar += new EventHandler(delegate { throw new Exception(); });
                        dynamic foo3 = MakeDynamicRow("World,Hello\r\n222,333\r\n");
                        dynamic foo4 = new ExpandoObject();
                        foo4.World = "456";
                        foo4.Hello = 789;
                        foo4.Bar = null;
                        foo4.Bar += new EventHandler(delegate { throw new Exception(); });

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo1);
                            await csv.WriteAsync(foo2);
                            await csv.WriteAsync(foo3);
                            await csv.WriteAsync(foo4);
                        }

                        var res = await getStr();

                        Assert.Equal("Hello,World\n123,456\n111,789\n333,222\n789,456", res);
                    }
                );
            }
        }

        [Fact]
        public async Task WriteCommentAsync()
        {
            var dynOpts = Options.CreateBuilder(Options.Default).WithReadHeader(ReadHeader.Never).ToOptions();

            // no trailing new line
            {
                // first line, no headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("");
                            }

                            var res = await getStr();
                            Assert.Equal("#", res);
                        }
                    );

                    // one line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello");
                            }

                            var res = await getStr();
                            Assert.Equal("#hello", res);
                        }
                    );

                    // two lines
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello\r\nworld");
                            }

                            var res = await getStr();
                            Assert.Equal("#hello\r\n#world", res);
                        }
                    );
                }

                // second line, no headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("");
                            }

                            var res = await getStr();
                            Assert.Equal("#", res);
                        }
                    );

                    // one line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello");
                            }

                            var res = await getStr();
                            Assert.Equal("123,456\r\n#hello", res);
                        }
                    );

                    // two lines
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello\r\nworld");
                            }

                            var res = await getStr();
                            Assert.Equal("123,456\r\n#hello\r\n#world", res);
                        }
                    );
                }

                // second line, headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Always).ToOptions();

                    // one line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello", res);
                        }
                    );

                    // two lines
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello\r\nworld");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello\r\n#world", res);
                        }
                    );
                }

                // before row, no headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("");
                            }

                            var res = await getStr();
                            Assert.Equal("#", res);
                        }
                    );

                    // one line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello");
                                await csv.WriteAsync(new { Foo = 123, Bar = 456 });
                            }

                            var res = await getStr();
                            Assert.Equal("#hello\r\n123,456", res);
                        }
                    );

                    // two lines
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello\r\nworld");
                                await csv.WriteAsync(new { Foo = 123, Bar = 456 });
                            }

                            var res = await getStr();
                            Assert.Equal("#hello\r\n#world\r\n123,456", res);
                        }
                    );
                }
            }

            // trailing new line
            {
                // first line, no headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("");
                            }

                            var res = await getStr();
                            Assert.Equal("#\r\n", res);
                        }
                    );

                    // one line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello");
                            }

                            var res = await getStr();
                            Assert.Equal("#hello\r\n", res);
                        }
                    );

                    // two lines
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello\r\nworld");
                            }

                            var res = await getStr();
                            Assert.Equal("#hello\r\n#world\r\n", res);
                        }
                    );
                }

                // second line, no headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("");
                            }

                            var res = await getStr();
                            Assert.Equal("#\r\n", res);
                        }
                    );

                    // one line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello");
                            }

                            var res = await getStr();
                            Assert.Equal("123,456\r\n#hello\r\n", res);
                        }
                    );

                    // two lines
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello\r\nworld");
                            }

                            var res = await getStr();
                            Assert.Equal("123,456\r\n#hello\r\n#world\r\n", res);
                        }
                    );
                }

                // second line, headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Always).ToOptions();

                    // one line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello\r\n", res);
                        }
                    );

                    // two lines
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello\r\nworld");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello\r\n#world\r\n", res);
                        }
                    );
                }

                // before row, no headers
                {
                    var opts = Options.CreateBuilder(dynOpts).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("");
                            }

                            var res = await getStr();
                            Assert.Equal("#\r\n", res);
                        }
                    );

                    // one line
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello");
                                await csv.WriteAsync(new { Foo = 123, Bar = 456 });
                            }

                            var res = await getStr();
                            Assert.Equal("#hello\r\n123,456\r\n", res);
                        }
                    );

                    // two lines
                    await RunAsyncDynamicWriterVariants(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello\r\nworld");
                                await csv.WriteAsync(new { Foo = 123, Bar = 456 });
                            }

                            var res = await getStr();
                            Assert.Equal("#hello\r\n#world\r\n123,456\r\n", res);
                        }
                    );
                }
            }
        }

        [Fact]
        public async Task NeedEscapeColumnNamesAsync()
        {
            // \r\n
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithReadHeader(ReadHeader.Always)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithWriteRowEnding(WriteRowEnding.CarriageReturnLineFeed)
                        .ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        dynamic foo = MakeDynamicRow("\"He,llo\",\"\"\"\"\r\n123,456");

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(foo);
                        }

                        foo.Dispose();

                        var res = await getStr();

                        Assert.Equal("\"He,llo\",\"\"\"\"\r\n123,456", res);
                    }
                );
            }
        }

        [Fact]
        public async Task CommentEscapeAsync()
        {
            // \r\n
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Never)
                        .WithWriteRowEnding(WriteRowEnding.CarriageReturnLineFeed)
                        .WithCommentCharacter('#')
                        .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always)
                        .ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(MakeDynamicRow("Hello,World\r\n#hello,foo"));
                        }

                        var txt = await getString();
                        Assert.Equal("\"#hello\",foo\r\n", txt);
                    }
                );

                await RunAsyncDynamicWriterVariants(
                    opts,
                     async (config, getWriter, getString) =>
                     {
                         await using (var writer = config.CreateAsyncWriter(getWriter()))
                         {
                             await writer.WriteAsync(MakeDynamicRow("Hello,World\r\nhello,fo#o"));
                         }

                         var txt = await getString();
                         Assert.Equal("hello,\"fo#o\"\r\n", txt);
                     }
                );
            }

            // \r
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Never)
                        .WithWriteRowEnding(WriteRowEnding.CarriageReturn)
                        .WithCommentCharacter('#')
                        .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always)
                        .ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                     async (config, getWriter, getString) =>
                     {
                         await using (var writer = config.CreateAsyncWriter(getWriter()))
                         {
                             await writer.WriteAsync(MakeDynamicRow("Hello,World\r\n#hello,foo"));
                         }

                         var txt = await getString();
                         Assert.Equal("\"#hello\",foo\r", txt);
                     }
                );

                await RunAsyncDynamicWriterVariants(
                    opts,
                     async (config, getWriter, getString) =>
                     {
                         await using (var writer = config.CreateAsyncWriter(getWriter()))
                         {
                             await writer.WriteAsync(MakeDynamicRow("Hello,World\r\nhello,fo#o"));
                         }

                         var txt = await getString();
                         Assert.Equal("hello,\"fo#o\"\r", txt);
                     }
                );
            }

            // \n
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Never)
                        .WithWriteRowEnding(WriteRowEnding.LineFeed)
                        .WithCommentCharacter('#')
                        .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always)
                        .ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                     async (config, getWriter, getString) =>
                     {
                         await using (var writer = config.CreateAsyncWriter(getWriter()))
                         {
                             await writer.WriteAsync(MakeDynamicRow("Hello,World\r\n#hello,foo"));
                         }

                         var txt = await getString();
                         Assert.Equal("\"#hello\",foo\n", txt);
                     }
                );

                await RunAsyncDynamicWriterVariants(
                    opts,
                     async (config, getWriter, getString) =>
                     {
                         await using (var writer = config.CreateAsyncWriter(getWriter()))
                         {
                             await writer.WriteAsync(MakeDynamicRow("Hello,World\r\nhello,fo#o"));
                         }

                         var txt = await getString();
                         Assert.Equal("hello,\"fo#o\"\n", txt);
                     }
                );
            }
        }

        [Fact]
        public async Task EscapeHeadersAsync()
        {
            // \r\n
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithWriteRowEnding(WriteRowEnding.CarriageReturnLineFeed)
                        .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always)
                        .ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            IDictionary<string, object> expando1 = new ExpandoObject();
                            expando1["hello\r\nworld"] = "fizz";
                            expando1["foo,bar"] = "buzz";
                            expando1["yup"] = "yes";

                            IDictionary<string, object> expando2 = new ExpandoObject();
                            expando2["hello\r\nworld"] = "ping";
                            expando2["foo,bar"] = "pong";
                            expando2["yup"] = "no";

                            await writer.WriteAsync(expando1);
                            await writer.WriteAsync(expando2);
                        }

                        var txt = await getString();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\r\nfizz,buzz,yes\r\nping,pong,no\r\n", txt);
                    }
                );
            }

            // \r
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithWriteRowEnding(WriteRowEnding.CarriageReturn)
                        .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always)
                        .ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            IDictionary<string, object> expando1 = new ExpandoObject();
                            expando1["hello\r\nworld"] = "fizz";
                            expando1["foo,bar"] = "buzz";
                            expando1["yup"] = "yes";

                            IDictionary<string, object> expando2 = new ExpandoObject();
                            expando2["hello\r\nworld"] = "ping";
                            expando2["foo,bar"] = "pong";
                            expando2["yup"] = "no";

                            await writer.WriteAsync(expando1);
                            await writer.WriteAsync(expando2);
                        }

                        var txt = await getString();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\rfizz,buzz,yes\rping,pong,no\r", txt);
                    }
                );
            }

            // \n
            {
                var opts =
                    Options.CreateBuilder(Options.DynamicDefault)
                        .WithWriteHeader(WriteHeader.Always)
                        .WithWriteRowEnding(WriteRowEnding.LineFeed)
                        .WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always)
                        .ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            IDictionary<string, object> expando1 = new ExpandoObject();
                            expando1["hello\r\nworld"] = "fizz";
                            expando1["foo,bar"] = "buzz";
                            expando1["yup"] = "yes";

                            IDictionary<string, object> expando2 = new ExpandoObject();
                            expando2["hello\r\nworld"] = "ping";
                            expando2["foo,bar"] = "pong";
                            expando2["yup"] = "no";

                            await writer.WriteAsync(expando1);
                            await writer.WriteAsync(expando2);
                        }

                        var txt = await getString();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\nfizz,buzz,yes\nping,pong,no\n", txt);
                    }
                );
            }
        }

        [Fact]
        public async Task NeedEscapeAsync()
        {
            // \r\n
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Never).WithWriteRowEnding(WriteRowEnding.CarriageReturnLineFeed).ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new { Foo = "hello,world", Bar = 123, Nope = 456 });
                            await writer.WriteAsync(new { Foo = "foo\"bar", Bar = 789, Nope = default(string) });
                            await writer.WriteAsync(new { Foo = "fizz\r\nbuzz", Bar = -12, Nope = 34 });
                        }

                        var txt = await getString();
                        Assert.Equal("\"hello,world\",123,456\r\n\"foo\"\"bar\",789,\r\n\"fizz\r\nbuzz\",-12,34", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Never).WithWriteRowEnding(WriteRowEnding.CarriageReturn).ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new { Foo = "hello,world", Bar = 123, Nope = 456 });
                            await writer.WriteAsync(new { Foo = "foo\"bar", Bar = 789, Nope = default(string) });
                            await writer.WriteAsync(new { Foo = "fizz\r\nbuzz", Bar = -12, Nope = 34 });
                        }

                        var txt = await getString();
                        Assert.Equal("\"hello,world\",123,456\r\"foo\"\"bar\",789,\r\"fizz\r\nbuzz\",-12,34", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Never).WithWriteRowEnding(WriteRowEnding.LineFeed).ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new { Foo = "hello,world", Bar = 123, Nope = 456 });
                            await writer.WriteAsync(new { Foo = "foo\"bar", Bar = 789, Nope = default(string) });
                            await writer.WriteAsync(new { Foo = "fizz\r\nbuzz", Bar = -12, Nope = 34 });
                        }

                        var txt = await getString();
                        Assert.Equal("\"hello,world\",123,456\n\"foo\"\"bar\",789,\n\"fizz\r\nbuzz\",-12,34", txt);
                    }
                );
            }

            // large
            {
                var opts = Options.DynamicDefault;
                var val = string.Join("", System.Linq.Enumerable.Repeat("abc\r\n", 450));

                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new { Foo = val, Bar = 001, Nope = 009 });
                        }

                        var txt = await getString();
                        Assert.Equal("Foo,Bar,Nope\r\n\"abc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\n\",1,9", txt);
                    }
                );
            }
        }

        [Fact]
        public async Task WriteAllAsync()
        {
            // \r\n
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Always).WithWriteRowEnding(WriteRowEnding.CarriageReturnLineFeed).ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAllAsync(
                                new object[]
                                {
                                    new { Foo = "hello", Bar = 123, Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Buzz = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero) },
                                    new { Bar = 456, Buzz = new DateTimeOffset(1980, 2, 2, 1, 1, 1, TimeSpan.Zero), Fizz = default(Guid?), Foo = "hello" },
                                    new { Bar = 789, Buzz = new DateTimeOffset(1990, 3, 3, 2, 2, 2, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = default(string) },
                                    new { Bar = 012, Buzz = new DateTimeOffset(2000, 4, 4, 3, 3, 3, TimeSpan.Zero), Fizz = default(Guid?), Foo = default(string) }
                                }
                            );
                        }

                        var txt = await getString();
                        Assert.Equal("Foo,Bar,Fizz,Buzz\r\nhello,123,5dc798f5-6477-4216-8567-9d17c05fa87e,1970-01-01 00:00:00Z\r\nhello,456,,1980-02-02 01:01:01Z\r\n,789,5dc798f5-6477-4216-8567-9d17c05fa87e,1990-03-03 02:02:02Z\r\n,12,,2000-04-04 03:03:03Z", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Always).WithWriteRowEnding(WriteRowEnding.CarriageReturn).ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAllAsync(
                                new object[]
                                {
                                    new { Foo = "hello", Bar = 123, Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Buzz = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero) },
                                    new { Bar = 456, Buzz = new DateTimeOffset(1980, 2, 2, 1, 1, 1, TimeSpan.Zero), Fizz = default(Guid?), Foo = "hello" },
                                    new { Bar = 789, Buzz = new DateTimeOffset(1990, 3, 3, 2, 2, 2, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = default(string) },
                                    new { Bar = 012, Buzz = new DateTimeOffset(2000, 4, 4, 3, 3, 3, TimeSpan.Zero), Fizz = default(Guid?), Foo = default(string) }
                                }
                            );
                        }

                        var txt = await getString();
                        Assert.Equal("Foo,Bar,Fizz,Buzz\rhello,123,5dc798f5-6477-4216-8567-9d17c05fa87e,1970-01-01 00:00:00Z\rhello,456,,1980-02-02 01:01:01Z\r,789,5dc798f5-6477-4216-8567-9d17c05fa87e,1990-03-03 02:02:02Z\r,12,,2000-04-04 03:03:03Z", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.CreateBuilder(Options.DynamicDefault).WithWriteHeader(WriteHeader.Always).WithWriteRowEnding(WriteRowEnding.LineFeed).ToOptions();

                await RunAsyncDynamicWriterVariants(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAllAsync(
                                new object[]
                                {
                                    new { Foo = "hello", Bar = 123, Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Buzz = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero) },
                                    new { Bar = 456, Buzz = new DateTimeOffset(1980, 2, 2, 1, 1, 1, TimeSpan.Zero), Fizz = default(Guid?), Foo = "hello" },
                                    new { Bar = 789, Buzz = new DateTimeOffset(1990, 3, 3, 2, 2, 2, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = default(string) },
                                    new { Bar = 012, Buzz = new DateTimeOffset(2000, 4, 4, 3, 3, 3, TimeSpan.Zero), Fizz = default(Guid?), Foo = default(string) }
                                }
                            );
                        }

                        var txt = await getString();
                        Assert.Equal("Foo,Bar,Fizz,Buzz\nhello,123,5dc798f5-6477-4216-8567-9d17c05fa87e,1970-01-01 00:00:00Z\nhello,456,,1980-02-02 01:01:01Z\n,789,5dc798f5-6477-4216-8567-9d17c05fa87e,1990-03-03 02:02:02Z\n,12,,2000-04-04 03:03:03Z", txt);
                    }
                );
            }
        }
    }
}
