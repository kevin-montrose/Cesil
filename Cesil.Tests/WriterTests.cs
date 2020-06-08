using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Pipelines;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Xunit;

using static Cesil.Tests.Helpers;

namespace Cesil.Tests
{
#pragma warning disable IDE1006
    public class WriterTests
    {
        private sealed class _ReturnedRowCounts
        {
            public string A { get; set; }
            public int B { get; set; }
        }

        [Fact]
        public void ReturnedRowCounts()
        {
            // simple
            RunSyncWriterVariants<_ReturnedRowCounts>(
                Options.Default,
                (config, getWriter, getStr) =>
                {
                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        var n1 =
                            csv.WriteAll(
                                new[]
                                {
                                    new _ReturnedRowCounts
                                    {
                                        A = "Foo",
                                        B = 123
                                    },
                                    new _ReturnedRowCounts
                                    {
                                        A = "Bar",
                                        B = 456
                                    },
                                    new _ReturnedRowCounts
                                    {
                                        A = "Fizz",
                                        B = 456
                                    }
                                }
                            );

                        Assert.Equal(3, n1);

                        var n2 = csv.WriteAll(new[] { new _ReturnedRowCounts { A = "Buzz", B = 789 } });
                        Assert.Equal(1, n2);
                    }

                    var res = getStr();
                    Assert.Equal("A,B\r\nFoo,123\r\nBar,456\r\nFizz,456\r\nBuzz,789", res);
                }
            );

            // no count
            RunSyncWriterVariants<(string, int)>(
                Options.Default,
                (config, getWriter, getStr) =>
                {
                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        var n1 =
                            csv.WriteAll(
                                Enumerable.Range(0, 1_000).Select(x => (A: x.ToString(), B: x * 2))
                            );

                        Assert.Equal(1_000, n1);

                        var n2 =
                            csv.WriteAll(
                                new[] { (A: "Fizz", B: -1) }
                            );
                        Assert.Equal(1, n2);
                    }

                    var res = getStr();
                    Assert.Equal("Item1,Item2\r\n0,0\r\n1,2\r\n2,4\r\n3,6\r\n4,8\r\n5,10\r\n6,12\r\n7,14\r\n8,16\r\n9,18\r\n10,20\r\n11,22\r\n12,24\r\n13,26\r\n14,28\r\n15,30\r\n16,32\r\n17,34\r\n18,36\r\n19,38\r\n20,40\r\n21,42\r\n22,44\r\n23,46\r\n24,48\r\n25,50\r\n26,52\r\n27,54\r\n28,56\r\n29,58\r\n30,60\r\n31,62\r\n32,64\r\n33,66\r\n34,68\r\n35,70\r\n36,72\r\n37,74\r\n38,76\r\n39,78\r\n40,80\r\n41,82\r\n42,84\r\n43,86\r\n44,88\r\n45,90\r\n46,92\r\n47,94\r\n48,96\r\n49,98\r\n50,100\r\n51,102\r\n52,104\r\n53,106\r\n54,108\r\n55,110\r\n56,112\r\n57,114\r\n58,116\r\n59,118\r\n60,120\r\n61,122\r\n62,124\r\n63,126\r\n64,128\r\n65,130\r\n66,132\r\n67,134\r\n68,136\r\n69,138\r\n70,140\r\n71,142\r\n72,144\r\n73,146\r\n74,148\r\n75,150\r\n76,152\r\n77,154\r\n78,156\r\n79,158\r\n80,160\r\n81,162\r\n82,164\r\n83,166\r\n84,168\r\n85,170\r\n86,172\r\n87,174\r\n88,176\r\n89,178\r\n90,180\r\n91,182\r\n92,184\r\n93,186\r\n94,188\r\n95,190\r\n96,192\r\n97,194\r\n98,196\r\n99,198\r\n100,200\r\n101,202\r\n102,204\r\n103,206\r\n104,208\r\n105,210\r\n106,212\r\n107,214\r\n108,216\r\n109,218\r\n110,220\r\n111,222\r\n112,224\r\n113,226\r\n114,228\r\n115,230\r\n116,232\r\n117,234\r\n118,236\r\n119,238\r\n120,240\r\n121,242\r\n122,244\r\n123,246\r\n124,248\r\n125,250\r\n126,252\r\n127,254\r\n128,256\r\n129,258\r\n130,260\r\n131,262\r\n132,264\r\n133,266\r\n134,268\r\n135,270\r\n136,272\r\n137,274\r\n138,276\r\n139,278\r\n140,280\r\n141,282\r\n142,284\r\n143,286\r\n144,288\r\n145,290\r\n146,292\r\n147,294\r\n148,296\r\n149,298\r\n150,300\r\n151,302\r\n152,304\r\n153,306\r\n154,308\r\n155,310\r\n156,312\r\n157,314\r\n158,316\r\n159,318\r\n160,320\r\n161,322\r\n162,324\r\n163,326\r\n164,328\r\n165,330\r\n166,332\r\n167,334\r\n168,336\r\n169,338\r\n170,340\r\n171,342\r\n172,344\r\n173,346\r\n174,348\r\n175,350\r\n176,352\r\n177,354\r\n178,356\r\n179,358\r\n180,360\r\n181,362\r\n182,364\r\n183,366\r\n184,368\r\n185,370\r\n186,372\r\n187,374\r\n188,376\r\n189,378\r\n190,380\r\n191,382\r\n192,384\r\n193,386\r\n194,388\r\n195,390\r\n196,392\r\n197,394\r\n198,396\r\n199,398\r\n200,400\r\n201,402\r\n202,404\r\n203,406\r\n204,408\r\n205,410\r\n206,412\r\n207,414\r\n208,416\r\n209,418\r\n210,420\r\n211,422\r\n212,424\r\n213,426\r\n214,428\r\n215,430\r\n216,432\r\n217,434\r\n218,436\r\n219,438\r\n220,440\r\n221,442\r\n222,444\r\n223,446\r\n224,448\r\n225,450\r\n226,452\r\n227,454\r\n228,456\r\n229,458\r\n230,460\r\n231,462\r\n232,464\r\n233,466\r\n234,468\r\n235,470\r\n236,472\r\n237,474\r\n238,476\r\n239,478\r\n240,480\r\n241,482\r\n242,484\r\n243,486\r\n244,488\r\n245,490\r\n246,492\r\n247,494\r\n248,496\r\n249,498\r\n250,500\r\n251,502\r\n252,504\r\n253,506\r\n254,508\r\n255,510\r\n256,512\r\n257,514\r\n258,516\r\n259,518\r\n260,520\r\n261,522\r\n262,524\r\n263,526\r\n264,528\r\n265,530\r\n266,532\r\n267,534\r\n268,536\r\n269,538\r\n270,540\r\n271,542\r\n272,544\r\n273,546\r\n274,548\r\n275,550\r\n276,552\r\n277,554\r\n278,556\r\n279,558\r\n280,560\r\n281,562\r\n282,564\r\n283,566\r\n284,568\r\n285,570\r\n286,572\r\n287,574\r\n288,576\r\n289,578\r\n290,580\r\n291,582\r\n292,584\r\n293,586\r\n294,588\r\n295,590\r\n296,592\r\n297,594\r\n298,596\r\n299,598\r\n300,600\r\n301,602\r\n302,604\r\n303,606\r\n304,608\r\n305,610\r\n306,612\r\n307,614\r\n308,616\r\n309,618\r\n310,620\r\n311,622\r\n312,624\r\n313,626\r\n314,628\r\n315,630\r\n316,632\r\n317,634\r\n318,636\r\n319,638\r\n320,640\r\n321,642\r\n322,644\r\n323,646\r\n324,648\r\n325,650\r\n326,652\r\n327,654\r\n328,656\r\n329,658\r\n330,660\r\n331,662\r\n332,664\r\n333,666\r\n334,668\r\n335,670\r\n336,672\r\n337,674\r\n338,676\r\n339,678\r\n340,680\r\n341,682\r\n342,684\r\n343,686\r\n344,688\r\n345,690\r\n346,692\r\n347,694\r\n348,696\r\n349,698\r\n350,700\r\n351,702\r\n352,704\r\n353,706\r\n354,708\r\n355,710\r\n356,712\r\n357,714\r\n358,716\r\n359,718\r\n360,720\r\n361,722\r\n362,724\r\n363,726\r\n364,728\r\n365,730\r\n366,732\r\n367,734\r\n368,736\r\n369,738\r\n370,740\r\n371,742\r\n372,744\r\n373,746\r\n374,748\r\n375,750\r\n376,752\r\n377,754\r\n378,756\r\n379,758\r\n380,760\r\n381,762\r\n382,764\r\n383,766\r\n384,768\r\n385,770\r\n386,772\r\n387,774\r\n388,776\r\n389,778\r\n390,780\r\n391,782\r\n392,784\r\n393,786\r\n394,788\r\n395,790\r\n396,792\r\n397,794\r\n398,796\r\n399,798\r\n400,800\r\n401,802\r\n402,804\r\n403,806\r\n404,808\r\n405,810\r\n406,812\r\n407,814\r\n408,816\r\n409,818\r\n410,820\r\n411,822\r\n412,824\r\n413,826\r\n414,828\r\n415,830\r\n416,832\r\n417,834\r\n418,836\r\n419,838\r\n420,840\r\n421,842\r\n422,844\r\n423,846\r\n424,848\r\n425,850\r\n426,852\r\n427,854\r\n428,856\r\n429,858\r\n430,860\r\n431,862\r\n432,864\r\n433,866\r\n434,868\r\n435,870\r\n436,872\r\n437,874\r\n438,876\r\n439,878\r\n440,880\r\n441,882\r\n442,884\r\n443,886\r\n444,888\r\n445,890\r\n446,892\r\n447,894\r\n448,896\r\n449,898\r\n450,900\r\n451,902\r\n452,904\r\n453,906\r\n454,908\r\n455,910\r\n456,912\r\n457,914\r\n458,916\r\n459,918\r\n460,920\r\n461,922\r\n462,924\r\n463,926\r\n464,928\r\n465,930\r\n466,932\r\n467,934\r\n468,936\r\n469,938\r\n470,940\r\n471,942\r\n472,944\r\n473,946\r\n474,948\r\n475,950\r\n476,952\r\n477,954\r\n478,956\r\n479,958\r\n480,960\r\n481,962\r\n482,964\r\n483,966\r\n484,968\r\n485,970\r\n486,972\r\n487,974\r\n488,976\r\n489,978\r\n490,980\r\n491,982\r\n492,984\r\n493,986\r\n494,988\r\n495,990\r\n496,992\r\n497,994\r\n498,996\r\n499,998\r\n500,1000\r\n501,1002\r\n502,1004\r\n503,1006\r\n504,1008\r\n505,1010\r\n506,1012\r\n507,1014\r\n508,1016\r\n509,1018\r\n510,1020\r\n511,1022\r\n512,1024\r\n513,1026\r\n514,1028\r\n515,1030\r\n516,1032\r\n517,1034\r\n518,1036\r\n519,1038\r\n520,1040\r\n521,1042\r\n522,1044\r\n523,1046\r\n524,1048\r\n525,1050\r\n526,1052\r\n527,1054\r\n528,1056\r\n529,1058\r\n530,1060\r\n531,1062\r\n532,1064\r\n533,1066\r\n534,1068\r\n535,1070\r\n536,1072\r\n537,1074\r\n538,1076\r\n539,1078\r\n540,1080\r\n541,1082\r\n542,1084\r\n543,1086\r\n544,1088\r\n545,1090\r\n546,1092\r\n547,1094\r\n548,1096\r\n549,1098\r\n550,1100\r\n551,1102\r\n552,1104\r\n553,1106\r\n554,1108\r\n555,1110\r\n556,1112\r\n557,1114\r\n558,1116\r\n559,1118\r\n560,1120\r\n561,1122\r\n562,1124\r\n563,1126\r\n564,1128\r\n565,1130\r\n566,1132\r\n567,1134\r\n568,1136\r\n569,1138\r\n570,1140\r\n571,1142\r\n572,1144\r\n573,1146\r\n574,1148\r\n575,1150\r\n576,1152\r\n577,1154\r\n578,1156\r\n579,1158\r\n580,1160\r\n581,1162\r\n582,1164\r\n583,1166\r\n584,1168\r\n585,1170\r\n586,1172\r\n587,1174\r\n588,1176\r\n589,1178\r\n590,1180\r\n591,1182\r\n592,1184\r\n593,1186\r\n594,1188\r\n595,1190\r\n596,1192\r\n597,1194\r\n598,1196\r\n599,1198\r\n600,1200\r\n601,1202\r\n602,1204\r\n603,1206\r\n604,1208\r\n605,1210\r\n606,1212\r\n607,1214\r\n608,1216\r\n609,1218\r\n610,1220\r\n611,1222\r\n612,1224\r\n613,1226\r\n614,1228\r\n615,1230\r\n616,1232\r\n617,1234\r\n618,1236\r\n619,1238\r\n620,1240\r\n621,1242\r\n622,1244\r\n623,1246\r\n624,1248\r\n625,1250\r\n626,1252\r\n627,1254\r\n628,1256\r\n629,1258\r\n630,1260\r\n631,1262\r\n632,1264\r\n633,1266\r\n634,1268\r\n635,1270\r\n636,1272\r\n637,1274\r\n638,1276\r\n639,1278\r\n640,1280\r\n641,1282\r\n642,1284\r\n643,1286\r\n644,1288\r\n645,1290\r\n646,1292\r\n647,1294\r\n648,1296\r\n649,1298\r\n650,1300\r\n651,1302\r\n652,1304\r\n653,1306\r\n654,1308\r\n655,1310\r\n656,1312\r\n657,1314\r\n658,1316\r\n659,1318\r\n660,1320\r\n661,1322\r\n662,1324\r\n663,1326\r\n664,1328\r\n665,1330\r\n666,1332\r\n667,1334\r\n668,1336\r\n669,1338\r\n670,1340\r\n671,1342\r\n672,1344\r\n673,1346\r\n674,1348\r\n675,1350\r\n676,1352\r\n677,1354\r\n678,1356\r\n679,1358\r\n680,1360\r\n681,1362\r\n682,1364\r\n683,1366\r\n684,1368\r\n685,1370\r\n686,1372\r\n687,1374\r\n688,1376\r\n689,1378\r\n690,1380\r\n691,1382\r\n692,1384\r\n693,1386\r\n694,1388\r\n695,1390\r\n696,1392\r\n697,1394\r\n698,1396\r\n699,1398\r\n700,1400\r\n701,1402\r\n702,1404\r\n703,1406\r\n704,1408\r\n705,1410\r\n706,1412\r\n707,1414\r\n708,1416\r\n709,1418\r\n710,1420\r\n711,1422\r\n712,1424\r\n713,1426\r\n714,1428\r\n715,1430\r\n716,1432\r\n717,1434\r\n718,1436\r\n719,1438\r\n720,1440\r\n721,1442\r\n722,1444\r\n723,1446\r\n724,1448\r\n725,1450\r\n726,1452\r\n727,1454\r\n728,1456\r\n729,1458\r\n730,1460\r\n731,1462\r\n732,1464\r\n733,1466\r\n734,1468\r\n735,1470\r\n736,1472\r\n737,1474\r\n738,1476\r\n739,1478\r\n740,1480\r\n741,1482\r\n742,1484\r\n743,1486\r\n744,1488\r\n745,1490\r\n746,1492\r\n747,1494\r\n748,1496\r\n749,1498\r\n750,1500\r\n751,1502\r\n752,1504\r\n753,1506\r\n754,1508\r\n755,1510\r\n756,1512\r\n757,1514\r\n758,1516\r\n759,1518\r\n760,1520\r\n761,1522\r\n762,1524\r\n763,1526\r\n764,1528\r\n765,1530\r\n766,1532\r\n767,1534\r\n768,1536\r\n769,1538\r\n770,1540\r\n771,1542\r\n772,1544\r\n773,1546\r\n774,1548\r\n775,1550\r\n776,1552\r\n777,1554\r\n778,1556\r\n779,1558\r\n780,1560\r\n781,1562\r\n782,1564\r\n783,1566\r\n784,1568\r\n785,1570\r\n786,1572\r\n787,1574\r\n788,1576\r\n789,1578\r\n790,1580\r\n791,1582\r\n792,1584\r\n793,1586\r\n794,1588\r\n795,1590\r\n796,1592\r\n797,1594\r\n798,1596\r\n799,1598\r\n800,1600\r\n801,1602\r\n802,1604\r\n803,1606\r\n804,1608\r\n805,1610\r\n806,1612\r\n807,1614\r\n808,1616\r\n809,1618\r\n810,1620\r\n811,1622\r\n812,1624\r\n813,1626\r\n814,1628\r\n815,1630\r\n816,1632\r\n817,1634\r\n818,1636\r\n819,1638\r\n820,1640\r\n821,1642\r\n822,1644\r\n823,1646\r\n824,1648\r\n825,1650\r\n826,1652\r\n827,1654\r\n828,1656\r\n829,1658\r\n830,1660\r\n831,1662\r\n832,1664\r\n833,1666\r\n834,1668\r\n835,1670\r\n836,1672\r\n837,1674\r\n838,1676\r\n839,1678\r\n840,1680\r\n841,1682\r\n842,1684\r\n843,1686\r\n844,1688\r\n845,1690\r\n846,1692\r\n847,1694\r\n848,1696\r\n849,1698\r\n850,1700\r\n851,1702\r\n852,1704\r\n853,1706\r\n854,1708\r\n855,1710\r\n856,1712\r\n857,1714\r\n858,1716\r\n859,1718\r\n860,1720\r\n861,1722\r\n862,1724\r\n863,1726\r\n864,1728\r\n865,1730\r\n866,1732\r\n867,1734\r\n868,1736\r\n869,1738\r\n870,1740\r\n871,1742\r\n872,1744\r\n873,1746\r\n874,1748\r\n875,1750\r\n876,1752\r\n877,1754\r\n878,1756\r\n879,1758\r\n880,1760\r\n881,1762\r\n882,1764\r\n883,1766\r\n884,1768\r\n885,1770\r\n886,1772\r\n887,1774\r\n888,1776\r\n889,1778\r\n890,1780\r\n891,1782\r\n892,1784\r\n893,1786\r\n894,1788\r\n895,1790\r\n896,1792\r\n897,1794\r\n898,1796\r\n899,1798\r\n900,1800\r\n901,1802\r\n902,1804\r\n903,1806\r\n904,1808\r\n905,1810\r\n906,1812\r\n907,1814\r\n908,1816\r\n909,1818\r\n910,1820\r\n911,1822\r\n912,1824\r\n913,1826\r\n914,1828\r\n915,1830\r\n916,1832\r\n917,1834\r\n918,1836\r\n919,1838\r\n920,1840\r\n921,1842\r\n922,1844\r\n923,1846\r\n924,1848\r\n925,1850\r\n926,1852\r\n927,1854\r\n928,1856\r\n929,1858\r\n930,1860\r\n931,1862\r\n932,1864\r\n933,1866\r\n934,1868\r\n935,1870\r\n936,1872\r\n937,1874\r\n938,1876\r\n939,1878\r\n940,1880\r\n941,1882\r\n942,1884\r\n943,1886\r\n944,1888\r\n945,1890\r\n946,1892\r\n947,1894\r\n948,1896\r\n949,1898\r\n950,1900\r\n951,1902\r\n952,1904\r\n953,1906\r\n954,1908\r\n955,1910\r\n956,1912\r\n957,1914\r\n958,1916\r\n959,1918\r\n960,1920\r\n961,1922\r\n962,1924\r\n963,1926\r\n964,1928\r\n965,1930\r\n966,1932\r\n967,1934\r\n968,1936\r\n969,1938\r\n970,1940\r\n971,1942\r\n972,1944\r\n973,1946\r\n974,1948\r\n975,1950\r\n976,1952\r\n977,1954\r\n978,1956\r\n979,1958\r\n980,1960\r\n981,1962\r\n982,1964\r\n983,1966\r\n984,1968\r\n985,1970\r\n986,1972\r\n987,1974\r\n988,1976\r\n989,1978\r\n990,1980\r\n991,1982\r\n992,1984\r\n993,1986\r\n994,1988\r\n995,1990\r\n996,1992\r\n997,1994\r\n998,1996\r\n999,1998\r\nFizz,-1", res);
                }
            );
        }

        class _MultiCharacterValueSeparators
        {
            public int A { get; set; }
            public string B { get; set; }
        }

        class _MultiCharacterValueSeparators_Headers
        {
            [DataMember(Name = "A#*#Escaped")]
            public int A { get; set; }
            public string B { get; set; }
        }

        [Fact]
        public void MultiCharacterValueSeparators()
        {
            var opts = Options.CreateBuilder(Options.Default).WithValueSeparator("#*#").ToOptions();

            // no escapes
            {
                RunSyncWriterVariants<_MultiCharacterValueSeparators>(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(new _MultiCharacterValueSeparators { A = 123, B = "foo" });
                            csv.Write(new _MultiCharacterValueSeparators { A = 456, B = "#" });
                            csv.Write(new _MultiCharacterValueSeparators { A = 789, B = "*" });
                        }

                        var res = getStr();
                        Assert.Equal("A#*#B\r\n123#*#foo\r\n456#*##\r\n789#*#*", res);
                    }
                );
            }

            // escapes
            {
                RunSyncWriterVariants<_MultiCharacterValueSeparators>(
                   opts,
                   (config, getWriter, getStr) =>
                   {
                       using (var writer = getWriter())
                       using (var csv = config.CreateWriter(writer))
                       {
                           csv.Write(new _MultiCharacterValueSeparators { A = 123, B = "foo#*#bar" });
                           csv.Write(new _MultiCharacterValueSeparators { A = 456, B = "#" });
                           csv.Write(new _MultiCharacterValueSeparators { A = 789, B = "*" });
                       }

                       var res = getStr();
                       Assert.Equal("A#*#B\r\n123#*#\"foo#*#bar\"\r\n456#*##\r\n789#*#*", res);
                   }
               );
            }

            // in headers
            {
                RunSyncWriterVariants<_MultiCharacterValueSeparators_Headers>(
                  opts,
                  (config, getWriter, getStr) =>
                  {
                      using (var writer = getWriter())
                      using (var csv = config.CreateWriter(writer))
                      {
                          csv.Write(new _MultiCharacterValueSeparators_Headers { A = 123, B = "foo#*#bar" });
                          csv.Write(new _MultiCharacterValueSeparators_Headers { A = 456, B = "#" });
                          csv.Write(new _MultiCharacterValueSeparators_Headers { A = 789, B = "*" });
                      }

                      var res = getStr();
                      Assert.Equal("\"A#*#Escaped\"#*#B\r\n123#*#\"foo#*#bar\"\r\n456#*##\r\n789#*#*", res);
                  }
              );
            }
        }

        private enum _WellKnownSingleColumns
        {
            Foo,
            Bar
        }

        [Flags]
        private enum _WellKnownSingleColumns_Flags
        {
            Foo = 1,
            Bar = 2,
            Fizz = 4
        }

        [Fact]
        public void WellKnownSingleColumns()
        {
            // bool
            {
                RunSyncWriterVariants<bool>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new[] { true, false, true });
                        }

                        var res = getStr();
                        Assert.Equal("Boolean\r\nTrue\r\nFalse\r\nTrue", res);
                    }
                );
            }

            // bool?
            {
                RunSyncWriterVariants<bool?>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new bool?[] { true, false, null });
                        }

                        var res = getStr();
                        Assert.Equal("NullableBoolean\r\nTrue\r\nFalse\r\n", res);
                    }
                );
            }

            // char
            {
                RunSyncWriterVariants<char>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new[] { 'a', 'b', 'c' });
                        }

                        var res = getStr();
                        Assert.Equal("Char\r\na\r\nb\r\nc", res);
                    }
                );
            }

            // char?
            {
                RunSyncWriterVariants<char?>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new char?[] { 'a', null, 'c' });
                        }

                        var res = getStr();
                        Assert.Equal("NullableChar\r\na\r\n\r\nc", res);
                    }
                );
            }

            // byte
            {
                RunSyncWriterVariants<byte>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new byte[] { 0, 128, 255 });
                        }

                        var res = getStr();
                        Assert.Equal("Byte\r\n0\r\n128\r\n255", res);
                    }
                );
            }

            // byte?
            {
                RunSyncWriterVariants<byte?>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new byte?[] { 0, null, 255 });
                        }

                        var res = getStr();
                        Assert.Equal("NullableByte\r\n0\r\n\r\n255", res);
                    }
                );
            }

            // sbyte
            {
                RunSyncWriterVariants<sbyte>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new sbyte[] { 0, -127, -2 });
                        }

                        var res = getStr();
                        Assert.Equal("SByte\r\n0\r\n-127\r\n-2", res);
                    }
                );
            }

            // sbyte?
            {
                RunSyncWriterVariants<sbyte?>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new sbyte?[] { null, -127, -2 });
                        }

                        var res = getStr();
                        Assert.Equal("NullableSByte\r\n\r\n-127\r\n-2", res);
                    }
                );
            }

            // short
            {
                RunSyncWriterVariants<short>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new short[] { 0, -9876, -16000 });
                        }

                        var res = getStr();
                        Assert.Equal("Int16\r\n0\r\n-9876\r\n-16000", res);
                    }
                );
            }

            // short?
            {
                RunSyncWriterVariants<short?>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new short?[] { 0, null, -16000 });
                        }

                        var res = getStr();
                        Assert.Equal("NullableInt16\r\n0\r\n\r\n-16000", res);
                    }
                );
            }

            // ushort
            {
                RunSyncWriterVariants<ushort>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new ushort[] { 0, 12345, 32000 });
                        }

                        var res = getStr();
                        Assert.Equal("UInt16\r\n0\r\n12345\r\n32000", res);
                    }
                );
            }

            // ushort?
            {
                RunSyncWriterVariants<ushort?>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new ushort?[] { null, 12345, 32000 });
                        }

                        var res = getStr();
                        Assert.Equal("NullableUInt16\r\n\r\n12345\r\n32000", res);
                    }
                );
            }

            // int
            {
                RunSyncWriterVariants<int>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new int[] { 0, 2000000, -15 });
                        }

                        var res = getStr();
                        Assert.Equal("Int32\r\n0\r\n2000000\r\n-15", res);
                    }
                );
            }

            // int?
            {
                RunSyncWriterVariants<int?>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new int?[] { null, 2000000, -15 });
                        }

                        var res = getStr();
                        Assert.Equal("NullableInt32\r\n\r\n2000000\r\n-15", res);
                    }
                );
            }

            // uint
            {
                RunSyncWriterVariants<uint>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new uint[] { 0, 2000000, 4_000_000_000 });
                        }

                        var res = getStr();
                        Assert.Equal("UInt32\r\n0\r\n2000000\r\n4000000000", res);
                    }
                );
            }

            // uint?
            {
                RunSyncWriterVariants<uint?>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new uint?[] { null, 2000000, 4_000_000_000 });
                        }

                        var res = getStr();
                        Assert.Equal("NullableUInt32\r\n\r\n2000000\r\n4000000000", res);
                    }
                );
            }

            // long
            {
                RunSyncWriterVariants<long>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new long[] { 0, long.MinValue, long.MaxValue });
                        }

                        var res = getStr();
                        Assert.Equal($"Int64\r\n0\r\n{long.MinValue}\r\n{long.MaxValue}", res);
                    }
                );
            }

            // long?
            {
                RunSyncWriterVariants<long?>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new long?[] { long.MinValue, null, long.MaxValue });
                        }

                        var res = getStr();
                        Assert.Equal($"NullableInt64\r\n{long.MinValue}\r\n\r\n{long.MaxValue}", res);
                    }
                );
            }

            // ulong
            {
                RunSyncWriterVariants<ulong>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new ulong[] { 0, 123, ulong.MaxValue });
                        }

                        var res = getStr();
                        Assert.Equal($"UInt64\r\n0\r\n123\r\n{ulong.MaxValue}", res);
                    }
                );
            }

            // ulong?
            {
                RunSyncWriterVariants<ulong?>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new ulong?[] { 0, null, ulong.MaxValue });
                        }

                        var res = getStr();
                        Assert.Equal($"NullableUInt64\r\n0\r\n\r\n{ulong.MaxValue}", res);
                    }
                );
            }

            // float
            {
                RunSyncWriterVariants<float>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new float[] { 0.12f, 123456789.0123f, -999999.88888f });
                        }

                        var res = getStr();
                        Assert.Equal("Single\r\n0.119999997\r\n123456792\r\n-999999.875", res);
                    }
                );
            }

            // float?
            {
                RunSyncWriterVariants<float?>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new float?[] { 0.12f, null, -999999.88888f });
                        }

                        var res = getStr();
                        Assert.Equal($"NullableSingle\r\n0.119999997\r\n\r\n-999999.875", res);
                    }
                );
            }

            // double
            {
                RunSyncWriterVariants<double>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new double[] { 0.12, 123456789.0123, -999999.88888 });
                        }

                        var res = getStr();
                        Assert.Equal("Double\r\n0.12\r\n123456789.0123\r\n-999999.88887999998", res);
                    }
                );
            }

            // double?
            {
                RunSyncWriterVariants<double?>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new double?[] { 0.12, null, -999999.88888 });
                        }

                        var res = getStr();
                        Assert.Equal($"NullableDouble\r\n0.12\r\n\r\n-999999.88887999998", res);
                    }
                );
            }

            // decimal
            {
                RunSyncWriterVariants<decimal>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new decimal[] { 0.12m, 123456789.0123m, -999999.88888m });
                        }

                        var res = getStr();
                        Assert.Equal($"Decimal\r\n0.12\r\n123456789.0123\r\n-999999.88888", res);
                    }
                );
            }

            // decimal?
            {
                RunSyncWriterVariants<decimal?>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new decimal?[] { 0.12m, null, -999999.88888m });
                        }

                        var res = getStr();
                        Assert.Equal($"NullableDecimal\r\n0.12\r\n\r\n-999999.88888", res);
                    }
                );
            }

            // string
            {
                RunSyncWriterVariants<string>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new string[] { "hello", null, "world" });
                        }

                        var res = getStr();
                        Assert.Equal($"String\r\nhello\r\n\r\nworld", res);
                    }
                );
            }

            // Version
            {
                RunSyncWriterVariants<Version>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new Version[] { new Version(1, 2), null, new Version(1, 2, 3, 4) });
                        }

                        var res = getStr();
                        Assert.Equal($"Version\r\n1.2\r\n\r\n1.2.3.4", res);
                    }
                );
            }

            // Uri
            {
                RunSyncWriterVariants<Uri>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new Uri[] { new Uri("http://example.com/"), null, new Uri("https://stackoverflow.com/questions") });
                        }

                        var res = getStr();
                        Assert.Equal($"Uri\r\nhttp://example.com/\r\n\r\nhttps://stackoverflow.com/questions", res);
                    }
                );
            }

            // enum
            {
                RunSyncWriterVariants<_WellKnownSingleColumns>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new _WellKnownSingleColumns[] { _WellKnownSingleColumns.Foo, _WellKnownSingleColumns.Bar, _WellKnownSingleColumns.Foo });
                        }

                        var res = getStr();
                        Assert.Equal($"_WellKnownSingleColumns\r\nFoo\r\nBar\r\nFoo", res);
                    }
                );
            }

            // enum?
            {
                RunSyncWriterVariants<_WellKnownSingleColumns?>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new _WellKnownSingleColumns?[] { _WellKnownSingleColumns.Foo, _WellKnownSingleColumns.Bar, null });
                        }

                        var res = getStr();
                        Assert.Equal($"Nullable_WellKnownSingleColumns\r\nFoo\r\nBar\r\n", res);
                    }
                );
            }

            // flags enum
            {
                RunSyncWriterVariants<_WellKnownSingleColumns_Flags>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new _WellKnownSingleColumns_Flags[] { _WellKnownSingleColumns_Flags.Foo | _WellKnownSingleColumns_Flags.Bar, _WellKnownSingleColumns_Flags.Bar, _WellKnownSingleColumns_Flags.Fizz });
                        }

                        var res = getStr();
                        Assert.Equal($"_WellKnownSingleColumns_Flags\r\n\"Foo, Bar\"\r\nBar\r\nFizz", res);
                    }
                );
            }

            // flags enum?
            {
                RunSyncWriterVariants<_WellKnownSingleColumns_Flags?>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(new _WellKnownSingleColumns_Flags?[] { _WellKnownSingleColumns_Flags.Foo | _WellKnownSingleColumns_Flags.Bar, null, _WellKnownSingleColumns_Flags.Fizz });
                        }

                        var res = getStr();
                        Assert.Equal($"Nullable_WellKnownSingleColumns_Flags\r\n\"Foo, Bar\"\r\n\r\nFizz", res);
                    }
                );
            }

            // DateTime
            {
                RunSyncWriterVariants<DateTime>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        var ci = CultureInfo.InvariantCulture;

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(
                                new DateTime[]
                                {
                                    DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc),
                                    new DateTime(2020, 04, 23, 0, 0, 0, DateTimeKind.Utc),
                                    DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc)
                                }
                            );
                        }

                        var res = getStr();
                        Assert.Equal($"DateTime\r\n{DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc).ToString("u", ci)}\r\n{new DateTime(2020, 04, 23, 0, 0, 0, DateTimeKind.Utc).ToString("u", ci)}\r\n{DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc).ToString("u", ci)}", res);
                    }
                );
            }

            // DateTime?
            {
                RunSyncWriterVariants<DateTime?>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        var ci = CultureInfo.InvariantCulture;

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(
                                new DateTime?[]
                                {
                                    DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc),
                                    null,
                                    DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc),
                                }
                            );
                        }

                        var res = getStr();
                        Assert.Equal($"NullableDateTime\r\n{DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc).ToString("u", ci)}\r\n\r\n{DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc).ToString("u", ci)}", res);
                    }
                );
            }

            // DateTimeOffset
            {
                RunSyncWriterVariants<DateTimeOffset>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        var ci = CultureInfo.InvariantCulture;

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(
                                new DateTimeOffset[]
                                {
                                    DateTimeOffset.MaxValue,
                                    new DateTimeOffset(2020, 04, 23, 0, 0, 0, TimeSpan.Zero),
                                    DateTimeOffset.MinValue,
                                }
                            );
                        }

                        var res = getStr();
                        Assert.Equal($"DateTimeOffset\r\n{DateTimeOffset.MaxValue.ToString("u", ci)}\r\n{new DateTimeOffset(2020, 04, 23, 0, 0, 0, TimeSpan.Zero).ToString("u", ci)}\r\n{DateTimeOffset.MinValue.ToString("u", ci)}", res);
                    }
                );
            }

            // DateTimeOffset?
            {
                RunSyncWriterVariants<DateTimeOffset?>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        var ci = CultureInfo.InvariantCulture;

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(
                                new DateTimeOffset?[]
                                {
                                    DateTimeOffset.MaxValue,
                                    null,
                                    DateTimeOffset.MinValue,
                                }
                            );
                        }

                        var res = getStr();
                        Assert.Equal($"NullableDateTimeOffset\r\n{DateTimeOffset.MaxValue.ToString("u", ci)}\r\n\r\n{DateTimeOffset.MinValue.ToString("u", ci)}", res);
                    }
                );
            }

            // Guid
            {
                RunSyncWriterVariants<Guid>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(
                                new Guid[]
                                {
                                    Guid.Parse("2E9348A1-C3D9-4A9C-95FF-D97591F91542"),
                                    Guid.Parse("ECB04C56-3042-4234-B757-6AC6E53E10C2")
                                }
                            );
                        }

                        var res = getStr();
                        Assert.Equal($"Guid\r\n2e9348a1-c3d9-4a9c-95ff-d97591f91542\r\necb04c56-3042-4234-b757-6ac6e53e10c2", res);
                    }
                );
            }

            // Guid?
            {
                RunSyncWriterVariants<Guid?>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(
                                new Guid?[]
                                {
                                    null,
                                    Guid.Parse("ECB04C56-3042-4234-B757-6AC6E53E10C2")
                                }
                            );
                        }

                        var res = getStr();
                        Assert.Equal($"NullableGuid\r\n\r\necb04c56-3042-4234-b757-6ac6e53e10c2", res);
                    }
                );
            }

            // TimeSpan
            {
                RunSyncWriterVariants<TimeSpan>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(
                                new TimeSpan[]
                                {
                                    TimeSpan.MaxValue,
                                    TimeSpan.FromMilliseconds(123456),
                                    TimeSpan.MaxValue
                                }
                            );
                        }

                        var res = getStr();
                        Assert.Equal($"TimeSpan\r\n{TimeSpan.MaxValue}\r\n{TimeSpan.FromMilliseconds(123456)}\r\n{TimeSpan.MaxValue}", res);
                    }
                );
            }

            // TimeSpan?
            {
                RunSyncWriterVariants<TimeSpan?>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(
                                new TimeSpan?[]
                                {
                                    TimeSpan.MaxValue,
                                    null,
                                    TimeSpan.MaxValue
                                }
                            );
                        }

                        var res = getStr();
                        Assert.Equal($"NullableTimeSpan\r\n{TimeSpan.MaxValue}\r\n\r\n{TimeSpan.MaxValue}", res);
                    }
                );
            }

            // Index
            {
                RunSyncWriterVariants<Index>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(
                                new Index[]
                                {
                                    ^1,
                                    (Index)2,
                                    ^3
                                }
                            );
                        }

                        var res = getStr();
                        Assert.Equal($"Index\r\n{^1}\r\n{(Index)2}\r\n{^3}", res);
                    }
                );
            }

            // Index?
            {
                RunSyncWriterVariants<Index?>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(
                                new Index?[]
                                {
                                    ^1,
                                    null,
                                    ^3
                                }
                            );
                        }

                        var res = getStr();
                        Assert.Equal($"NullableIndex\r\n{^1}\r\n\r\n{^3}", res);
                    }
                );
            }

            // Range
            {
                RunSyncWriterVariants<Range>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(
                                new Range[]
                                {
                                    1..^1,
                                    ..^2,
                                    ^3..
                                }
                            );
                        }

                        var res = getStr();
                        Assert.Equal($"Range\r\n{1..^1}\r\n{..^2}\r\n{^3..}", res);
                    }
                );
            }

            // Range?
            {
                RunSyncWriterVariants<Range?>(
                    Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.WriteAll(
                                new Range?[]
                                {
                                    1..^1,
                                    null,
                                    ^3..
                                }
                            );
                        }

                        var res = getStr();
                        Assert.Equal($"NullableRange\r\n{1..^1}\r\n\r\n{^3..}", res);
                    }
                );
            }
        }

        private sealed class _DontEmitDefaultNonTrivial_TypeDescriber : DefaultTypeDescriber
        {
            protected override Formatter GetFormatter(TypeInfo forType, PropertyInfo property)
            {
                if (property.PropertyType == typeof(_DontEmitDefaultNonTrivial_Member).GetTypeInfo())
                {
                    return
                        Formatter.ForDelegate(
                            (_DontEmitDefaultNonTrivial_Member value, in WriteContext context, IBufferWriter<char> writer) =>
                            {
                                var span = writer.GetSpan(100);

                                if (!value.Fizz.TryFormat(span, out int written))
                                {
                                    throw new Exception();
                                }

                                writer.Advance(written);

                                return true;
                            }
                        );
                }

                return base.GetFormatter(forType, property);
            }
        }

        private sealed class _DontEmitDefaultNonTrivial_Member
        {
            public int Fizz { get; }

            public _DontEmitDefaultNonTrivial_Member(string a)
            {
                Fizz = a.Length;
            }
        }

        private sealed class _DontEmitDefaultNonTrivial
        {
            [DataMember(EmitDefaultValue = false)]
            public _DontEmitDefaultNonTrivial_Member Bar { get; }

            public _DontEmitDefaultNonTrivial(string foo)
            {
                Bar = foo != null ? new _DontEmitDefaultNonTrivial_Member(foo) : null;
            }
        }

        [Fact]
        public void DontEmitDefaultNonTrivial()
        {
            var data =
                new[]
                {
                    new _DontEmitDefaultNonTrivial("a"),
                    new _DontEmitDefaultNonTrivial(null),
                    new _DontEmitDefaultNonTrivial("ab"),
                    new _DontEmitDefaultNonTrivial("abc"),
                };

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(new _DontEmitDefaultNonTrivial_TypeDescriber()).ToOptions();

            RunSyncWriterVariants<_DontEmitDefaultNonTrivial>(
                opts,
                (config, getWriter, getStr) =>
                {
                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.WriteAll(data);
                    }

                    var res = getStr();
                    Assert.Equal("Bar\r\n1\r\n\r\n2\r\n3", res);
                }
            );
        }

        private sealed class _VariousShouldSerializes
        {
            public int Foo { get; set; }

            public static bool Row_Context(_VariousShouldSerializes row, ref WriteContext _)
            => true;

            public string Bar { get; set; }

            public static bool NoRow_Context(ref WriteContext _)
            => true;

            public bool Fizz { get; set; }

            public bool Context(ref WriteContext _)
            => true;
        }

        [Fact]
        public void VariousShouldSerializes()
        {
            var t = typeof(_VariousShouldSerializes).GetTypeInfo();

            var foo = Getter.ForProperty(t.GetProperty(nameof(_VariousShouldSerializes.Foo)));
            var bar = Getter.ForProperty(t.GetProperty(nameof(_VariousShouldSerializes.Bar)));
            var fizz = Getter.ForProperty(t.GetProperty(nameof(_VariousShouldSerializes.Fizz)));

            var fooShould = Cesil.ShouldSerialize.ForMethod(t.GetMethod(nameof(_VariousShouldSerializes.Row_Context)));
            var barShould = Cesil.ShouldSerialize.ForMethod(t.GetMethod(nameof(_VariousShouldSerializes.NoRow_Context)));
            var fizzShould = Cesil.ShouldSerialize.ForMethod(t.GetMethod(nameof(_VariousShouldSerializes.Context)));

            var m = ManualTypeDescriber.CreateBuilder();
            m.WithExplicitGetter(t, "a", foo, Formatter.GetDefault(typeof(int).GetTypeInfo()), fooShould);
            m.WithExplicitGetter(t, "b", bar, Formatter.GetDefault(typeof(string).GetTypeInfo()), barShould);
            m.WithExplicitGetter(t, "c", fizz, Formatter.GetDefault(typeof(bool).GetTypeInfo()), fizzShould);

            var td = m.ToManualTypeDescriber();

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(td).ToOptions();

            RunSyncWriterVariants<_VariousShouldSerializes>(
                opts,
                (config, getWriter, getStr) =>
                {
                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.Write(new _VariousShouldSerializes { Foo = 123, Bar = "hello", Fizz = false });
                        csv.Write(new _VariousShouldSerializes { Foo = 456, Bar = "world", Fizz = true });
                    }

                    var str = getStr();
                    Assert.Equal("a,b,c\r\n123,hello,False\r\n456,world,True", str);
                }
            );
        }

        [Fact]
        public void Tuples()
        {
            // Tuple
            RunSyncWriterVariants<Tuple<int, string, Guid>>(
                Options.Default,
                (config, getWriter, getStr) =>
                {
                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.Write(Tuple.Create(123, "hello", Guid.Parse("6ECB2B6D-D392-4222-B7F1-07E66A9A4259")));
                        csv.Write(Tuple.Create(456, "world", Guid.Parse("7ECB2B6D-D492-4223-B7F2-7E66A9B42590")));
                    }

                    var str = getStr();
                    Assert.Equal("Item1,Item2,Item3\r\n123,hello,6ecb2b6d-d392-4222-b7f1-07e66a9a4259\r\n456,world,7ecb2b6d-d492-4223-b7f2-7e66a9b42590", str);
                }
            );

            // Big Tuple
            Assert.Throws<InvalidOperationException>(() => Configuration.For<Tuple<int, int, int, int, int, int, int, Tuple<int, int>>>());

            // ValueTuple
            RunSyncWriterVariants<ValueTuple<int, string, Guid>>(
                Options.Default,
                (config, getWriter, getStr) =>
                {
                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.Write(ValueTuple.Create(123, "hello", Guid.Parse("6ECB2B6D-D392-4222-B7F1-07E66A9A4259")));
                        csv.Write(ValueTuple.Create(456, "world", Guid.Parse("7ECB2B6D-D492-4223-B7F2-7E66A9B42590")));
                    }

                    var str = getStr();
                    Assert.Equal("Item1,Item2,Item3\r\n123,hello,6ecb2b6d-d392-4222-b7f1-07e66a9a4259\r\n456,world,7ecb2b6d-d492-4223-b7f2-7e66a9b42590", str);
                }
            );

            // Big ValueTuple
            Assert.Throws<InvalidOperationException>(() => Configuration.For<ValueTuple<int, int, int, int, int, int, int, ValueTuple<int, int>>>());
        }

        [Fact]
        public void AnonymousType()
        {
            Thunk(
                new { Foo = "hello world", Bar = 123 },
                "Foo,Bar\r\nhello world,123"
            );

            // to provide a way to get an anonymous generic type for the helpers
            static void Thunk<T>(T ex, string expect)
            {
                // Tuple
                RunSyncWriterVariants<T>(
                    global::Cesil.Options.Default,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(ex);
                        }

                        var str = getStr();
                        Assert.Equal(expect, str);
                    }
                );
            }
        }

        private sealed class _ChainedFormatters
        {
            public string Foo { get; set; }
            public string Bar { get; set; }
            public string Fizz { get; set; }
            public string Buzz { get; set; }
        }

        private sealed class _ChainedFormatters_Context
        {
            public int F { get; set; }
            public int B { get; set; }
            public int Fi { get; set; }
            public int Bu { get; set; }
        }

        [Fact]
        public void ChainedFormatters()
        {
            var ip = InstanceProvider.ForDelegate<_ChainedFormatters>((in ReadContext _, out _ChainedFormatters x) => { x = new _ChainedFormatters(); return true; });
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

            var m =
                ManualTypeDescriberBuilder
                    .CreateBuilder()
                    .WithInstanceProvider(ip)
                    .WithSerializableProperty(typeof(_ChainedFormatters).GetProperty(nameof(_ChainedFormatters.Foo)), "Foo", f)
                    .ToManualTypeDescriber();

            var opts = OptionsBuilder.CreateBuilder(Options.Default).WithTypeDescriber(m).ToOptions();

            RunSyncWriterVariants<_ChainedFormatters>(
                opts,
                (config, getWriter, getStr) =>
                {
                    var ctx = new _ChainedFormatters_Context();

                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer, ctx))
                    {
                        ctx.F = 1;
                        csv.Write(new _ChainedFormatters { });
                        ctx.F = 2;
                        csv.Write(new _ChainedFormatters { });
                        ctx.F = 3;
                        csv.Write(new _ChainedFormatters { });
                        ctx.F = 1;
                        csv.Write(new _ChainedFormatters { });
                    }

                    var str = getStr();
                    Assert.Equal("Foo\r\n1234\r\nabc\r\n00\r\n1234", str);
                }
            );
        }

        [Fact]
        public void CheckCanEncode()
        {
            // single span
            {
                // default options can always encode
                WriterBase<object>.CheckCanEncode("hello", Options.Default);

                var tsv = Options.CreateBuilder(Options.Default).WithEscapedValueEscapeCharacter(null).WithValueSeparator("\t").ToOptions();
                // but " isn't
                var exc1 = Assert.Throws<InvalidOperationException>(() => WriterBase<object>.CheckCanEncode("\"", tsv));
                Assert.Contains("'\"'", exc1.Message);

                var noEscape = Options.CreateBuilder(Options.Default).WithEscapedValueEscapeCharacter(null).WithEscapedValueStartAndEnd(null).ToOptions();
                // comma is not escapable
                var exc2 = Assert.Throws<InvalidOperationException>(() => WriterBase<object>.CheckCanEncode(",", noEscape));
                Assert.Contains("','", exc2.Message);
            }

            // single span, multi-char
            {
                var opt = Options.CreateBuilder(Options.Default).WithValueSeparator("---").ToOptions();

                // default options can always encode
                WriterBase<object>.CheckCanEncode("hello", opt);

                var noInnerEscape = Options.CreateBuilder(opt).WithEscapedValueEscapeCharacter(null).ToOptions();
                // but " isn't
                var exc1 = Assert.Throws<InvalidOperationException>(() => WriterBase<object>.CheckCanEncode("\"", noInnerEscape));
                Assert.Contains("'\"'", exc1.Message);

                var noEscape = Options.CreateBuilder(opt).WithEscapedValueEscapeCharacter(null).WithEscapedValueStartAndEnd(null).ToOptions();
                // comma is not escapable
                var exc2 = Assert.Throws<InvalidOperationException>(() => WriterBase<object>.CheckCanEncode("---", noEscape));
                Assert.Contains("'---'", exc2.Message);
            }

            // sequence of single span
            {
                // default options can always encode
                var seq1 = new ReadOnlySequence<char>("hello".AsMemory());
                Assert.True(seq1.IsSingleSegment);
                WriterBase<object>.CheckCanEncode(seq1, Options.Default);

                var tsv = Options.CreateBuilder(Options.Default).WithEscapedValueEscapeCharacter(null).WithValueSeparator("\t").ToOptions();
                // but " isn't
                var seq2 = new ReadOnlySequence<char>("\"".AsMemory());
                Assert.True(seq2.IsSingleSegment);
                var exc1 = Assert.Throws<InvalidOperationException>(() => WriterBase<object>.CheckCanEncode(seq2, tsv));
                Assert.Contains("'\"'", exc1.Message);

                var noEscape = Options.CreateBuilder(Options.Default).WithEscapedValueEscapeCharacter(null).WithEscapedValueStartAndEnd(null).ToOptions();
                // comma is not escapable
                var seq3 = new ReadOnlySequence<char>(",".AsMemory());
                Assert.True(seq3.IsSingleSegment);
                var exc2 = Assert.Throws<InvalidOperationException>(() => WriterBase<object>.CheckCanEncode(",", noEscape));
                Assert.Contains("','", exc2.Message);
            }

            // sequence of single span, multi-char
            {
                var opt = Options.CreateBuilder(Options.Default).WithValueSeparator("---").ToOptions();

                // default options can always encode
                var seq1 = new ReadOnlySequence<char>("hello".AsMemory());
                Assert.True(seq1.IsSingleSegment);
                WriterBase<object>.CheckCanEncode(seq1, opt);

                var noInnerEscape = Options.CreateBuilder(opt).WithEscapedValueEscapeCharacter(null).ToOptions();
                // but " isn't
                var seq2 = new ReadOnlySequence<char>("\"".AsMemory());
                Assert.True(seq2.IsSingleSegment);
                var exc1 = Assert.Throws<InvalidOperationException>(() => WriterBase<object>.CheckCanEncode(seq2, noInnerEscape));
                Assert.Contains("'\"'", exc1.Message);

                var noEscape = Options.CreateBuilder(opt).WithEscapedValueEscapeCharacter(null).WithEscapedValueStartAndEnd(null).ToOptions();
                // comma is not escapable
                var seq3 = new ReadOnlySequence<char>("---".AsMemory());
                Assert.True(seq3.IsSingleSegment);
                var exc2 = Assert.Throws<InvalidOperationException>(() => WriterBase<object>.CheckCanEncode(seq3, noEscape));
                Assert.Contains("'---'", exc2.Message);
            }

            // sequence of multiple spans
            {
                // default options can always encode
                var seq1 = Split("hel-lo".AsMemory(), "-".AsMemory());
                Assert.False(seq1.IsSingleSegment);
                WriterBase<object>.CheckCanEncode(seq1, Options.Default);

                var tsv = Options.CreateBuilder(Options.Default).WithEscapedValueEscapeCharacter(null).WithValueSeparator("\t").ToOptions();
                // but " isn't
                var seq2 = Split("\" -".AsMemory(), " ".AsMemory());
                Assert.False(seq2.IsSingleSegment);
                var exc1 = Assert.Throws<InvalidOperationException>(() => WriterBase<object>.CheckCanEncode(seq2, tsv));
                Assert.Contains("'\"'", exc1.Message);

                var noEscape = Options.CreateBuilder(Options.Default).WithEscapedValueEscapeCharacter(null).WithEscapedValueStartAndEnd(null).ToOptions();
                // comma is not escapable
                var seq3 = Split("----!, ".AsMemory(), "!".AsMemory());
                Assert.False(seq3.IsSingleSegment);
                var exc2 = Assert.Throws<InvalidOperationException>(() => WriterBase<object>.CheckCanEncode(seq3, noEscape));
                Assert.Contains("','", exc2.Message);
            }

            // sequence of multiple spans, multi-char
            {
                var opt = Options.CreateBuilder(Options.Default).WithValueSeparator("---").ToOptions();

                // default options can always encode
                var seq1 = Split("hel-lo".AsMemory(), "-".AsMemory());
                Assert.False(seq1.IsSingleSegment);
                WriterBase<object>.CheckCanEncode(seq1, opt);

                var noInnerEscape = Options.CreateBuilder(opt).WithEscapedValueEscapeCharacter(null).ToOptions();
                // but " isn't
                var seq2 = Split("\" - ".AsMemory(), "-".AsMemory());
                Assert.False(seq2.IsSingleSegment);
                var exc1 = Assert.Throws<InvalidOperationException>(() => WriterBase<object>.CheckCanEncode(seq2, noInnerEscape));
                Assert.Contains("'\"'", exc1.Message);

                var noEscape = Options.CreateBuilder(opt).WithEscapedValueEscapeCharacter(null).WithEscapedValueStartAndEnd(null).ToOptions();
                // comma is not escapable
                var seq3 = Split("--- !".AsMemory(), " ".AsMemory());
                Assert.False(seq3.IsSingleSegment);
                var exc2 = Assert.Throws<InvalidOperationException>(() => WriterBase<object>.CheckCanEncode(seq3, noEscape));
                Assert.Contains("'---'", exc2.Message);
            }
        }

        private sealed class _NoEscapes
        {
            public string Foo { get; set; }
            public string Bar { get; set; }
        }

        [Fact]
        public void NoEscapes()
        {
            // no escapes at all (TSV)
            {
                var opts = OptionsBuilder.CreateBuilder(Options.Default).WithValueSeparator("\t").WithEscapedValueStartAndEnd(null).WithEscapedValueEscapeCharacter(null).ToOptions();

                // correct
                RunSyncWriterVariants<_NoEscapes>(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(new _NoEscapes { Foo = "abc", Bar = "123" });
                            csv.Write(new _NoEscapes { Foo = "\"", Bar = "," });
                        }

                        var str = getStr();
                        Assert.Equal("Foo\tBar\r\nabc\t123\r\n\"\t,", str);
                    }
                );

                // explodes if there's an value separator in a value, since there are no escapes
                {
                    // \t
                    RunSyncWriterVariants<_NoEscapes>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                var inv = Assert.Throws<InvalidOperationException>(() => csv.Write(new _NoEscapes { Foo = "\t", Bar = "foo" }));

                                Assert.Equal("Tried to write a value contain '\t' which requires escaping a value, but no way to escape a value is configured", inv.Message);
                            }

                            getStr();
                        }
                    );

                    // \r\n
                    RunSyncWriterVariants<_NoEscapes>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                var inv = Assert.Throws<InvalidOperationException>(() => csv.Write(new _NoEscapes { Foo = "foo", Bar = "\r\n" }));

                                Assert.Equal("Tried to write a value contain '\r' which requires escaping a value, but no way to escape a value is configured", inv.Message);
                            }

                            getStr();
                        }
                    );

                    var optsWithComment = OptionsBuilder.CreateBuilder(opts).WithCommentCharacter('#').ToOptions();
                    // #
                    RunSyncWriterVariants<_NoEscapes>(
                        optsWithComment,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                var inv = Assert.Throws<InvalidOperationException>(() => csv.Write(new _NoEscapes { Foo = "#", Bar = "fizz" }));

                                Assert.Equal("Tried to write a value contain '#' which requires escaping a value, but no way to escape a value is configured", inv.Message);
                            }

                            getStr();
                        }
                    );
                }
            }

            // escapes, but no escape for the escape start and end char
            {
                var opts = OptionsBuilder.CreateBuilder(Options.Default).WithValueSeparator("\t").WithEscapedValueStartAndEnd('"').WithEscapedValueEscapeCharacter(null).ToOptions();

                // correct
                RunSyncWriterVariants<_NoEscapes>(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(new _NoEscapes { Foo = "a\tbc", Bar = "#123" });
                            csv.Write(new _NoEscapes { Foo = "\r", Bar = "," });
                        }

                        var str = getStr();
                        Assert.Equal("Foo\tBar\r\n\"a\tbc\"\t#123\r\n\"\r\"\t,", str);
                    }
                );

                var optsWithComments = OptionsBuilder.CreateBuilder(opts).WithCommentCharacter('#').ToOptions();

                // correct with comments
                RunSyncWriterVariants<_NoEscapes>(
                    optsWithComments,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(new _NoEscapes { Foo = "a\tbc", Bar = "#123" });
                            csv.Write(new _NoEscapes { Foo = "\r", Bar = "," });
                        }

                        var str = getStr();
                        Assert.Equal("Foo\tBar\r\n\"a\tbc\"\t\"#123\"\r\n\"\r\"\t,", str);
                    }
                );

                // explodes if there's an escape start character in a value, since it can't be escaped
                RunSyncWriterVariants<_NoEscapes>(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            var inv = Assert.Throws<InvalidOperationException>(() => csv.Write(new _NoEscapes { Foo = "a\tbc", Bar = "\"" }));

                            Assert.Equal("Tried to write a value contain '\"' which requires escaping the character in an escaped value, but no way to escape inside an escaped value is configured", inv.Message);
                        }

                        getStr();
                    }
                );
            }
        }

        [Fact]
        public void NoEscapesMultiCharacterSeparator()
        {
            // no escapes at all (TSV)
            {
                var opts = OptionsBuilder.CreateBuilder(Options.Default).WithValueSeparator("**").WithEscapedValueStartAndEnd(null).WithEscapedValueEscapeCharacter(null).ToOptions();

                // correct
                RunSyncWriterVariants<_NoEscapes>(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(new _NoEscapes { Foo = "abc", Bar = "123" });
                            csv.Write(new _NoEscapes { Foo = "\"", Bar = "," });
                        }

                        var str = getStr();
                        Assert.Equal("Foo**Bar\r\nabc**123\r\n\"**,", str);
                    }
                );

                // explodes if there's an value separator in a value, since there are no escapes
                {
                    // \t
                    RunSyncWriterVariants<_NoEscapes>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                var inv = Assert.Throws<InvalidOperationException>(() => csv.Write(new _NoEscapes { Foo = "h**lo", Bar = "foo" }));

                                Assert.Equal("Tried to write a value contain '**' which requires escaping a value, but no way to escape a value is configured", inv.Message);
                            }

                            getStr();
                        }
                    );

                    // \r\n
                    RunSyncWriterVariants<_NoEscapes>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                var inv = Assert.Throws<InvalidOperationException>(() => csv.Write(new _NoEscapes { Foo = "foo", Bar = "\r\n" }));

                                Assert.Equal("Tried to write a value contain '\r' which requires escaping a value, but no way to escape a value is configured", inv.Message);
                            }

                            getStr();
                        }
                    );

                    var optsWithComment = OptionsBuilder.CreateBuilder(opts).WithCommentCharacter('#').ToOptions();
                    // #
                    RunSyncWriterVariants<_NoEscapes>(
                        optsWithComment,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                var inv = Assert.Throws<InvalidOperationException>(() => csv.Write(new _NoEscapes { Foo = "#", Bar = "fizz" }));

                                Assert.Equal("Tried to write a value contain '#' which requires escaping a value, but no way to escape a value is configured", inv.Message);
                            }

                            getStr();
                        }
                    );
                }
            }

            // escapes, but no escape for the escape start and end char
            {
                var opts = OptionsBuilder.CreateBuilder(Options.Default).WithValueSeparator("**").WithEscapedValueStartAndEnd('"').WithEscapedValueEscapeCharacter(null).ToOptions();

                // correct
                RunSyncWriterVariants<_NoEscapes>(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(new _NoEscapes { Foo = "a**bc", Bar = "#123" });
                            csv.Write(new _NoEscapes { Foo = "\r", Bar = "," });
                        }

                        var str = getStr();
                        Assert.Equal("Foo**Bar\r\n\"a**bc\"**#123\r\n\"\r\"**,", str);
                    }
                );

                var optsWithComments = OptionsBuilder.CreateBuilder(opts).WithCommentCharacter('#').ToOptions();

                // correct with comments
                RunSyncWriterVariants<_NoEscapes>(
                    optsWithComments,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            csv.Write(new _NoEscapes { Foo = "a**bc", Bar = "#123" });
                            csv.Write(new _NoEscapes { Foo = "\r", Bar = "," });
                        }

                        var str = getStr();
                        Assert.Equal("Foo**Bar\r\n\"a**bc\"**\"#123\"\r\n\"\r\"**,", str);
                    }
                );

                // explodes if there's an escape start character in a value, since it can't be escaped
                RunSyncWriterVariants<_NoEscapes>(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer))
                        {
                            var inv = Assert.Throws<InvalidOperationException>(() => csv.Write(new _NoEscapes { Foo = "a**bc", Bar = "\"" }));

                            Assert.Equal("Tried to write a value contain '\"' which requires escaping the character in an escaped value, but no way to escape inside an escaped value is configured", inv.Message);
                        }

                        getStr();
                    }
                );
            }
        }

        private sealed class _Errors
        {
            public string Foo { get; set; }
        }

        [Fact]
        public void Errors()
        {
            RunSyncWriterVariants<_Errors>(
                Options.Default,
                (config, makeWriter, getStr) =>
                {
                    using (var w = makeWriter())
                    using (var csv = config.CreateWriter(w))
                    {
                        Assert.Throws<ArgumentNullException>(() => csv.WriteAll(null));

                        var exc = Assert.Throws<InvalidOperationException>(() => csv.WriteComment("foo"));
                        Assert.Equal($"No {nameof(Options.CommentCharacter)} configured, cannot write a comment line", exc.Message);
                    }

                    getStr();
                }
            );
        }

        private sealed class _BufferWriterByte
        {
            public string Foo { get; set; }
            public string Bar { get; set; }
        }

        [Fact]
        public void BufferWriterByte()
        {
            var pipe = new Pipe();

            var config = Configuration.For<_BufferWriterByte>();
            using (var writer = config.CreateWriter(pipe.Writer, Encoding.UTF7))
            {
                writer.Write(new _BufferWriterByte { Foo = "hello", Bar = "world" });
            }

            pipe.Writer.Complete();

            var bytes = new List<byte>();

            while (pipe.Reader.TryRead(out var res))
            {
                foreach (var b in res.Buffer)
                {
                    bytes.AddRange(b.ToArray());
                }

                if (res.IsCompleted)
                {
                    break;
                }
            }

            var str = Encoding.UTF7.GetString(bytes.ToArray());

            Assert.Equal("Foo,Bar\r\nhello,world", str);
        }

        private sealed class _BufferWriterChar
        {
            public string Foo { get; set; }
            public string Bar { get; set; }
        }

        private sealed class _BufferWriterChar_Writer : IBufferWriter<char>
        {
            public List<char> Data;

            private Memory<char> Current;

            public _BufferWriterChar_Writer()
            {
                Data = new List<char>();
            }

            public void Advance(int count)
            {
                Data.AddRange(Current.Slice(0, count).ToArray());
                Current = Memory<char>.Empty;
            }

            public Memory<char> GetMemory(int sizeHint = 0)
            {
                if (sizeHint <= 0) sizeHint = 8;
                var arr = new char[sizeHint];

                Current = arr.AsMemory();

                return Current;
            }

            public Span<char> GetSpan(int sizeHint = 0)
            => GetMemory(sizeHint).Span;
        }

        [Fact]
        public void BufferWriterChar()
        {
            var charWriter = new _BufferWriterChar_Writer();

            var config = Configuration.For<_BufferWriterByte>();
            using (var writer = config.CreateWriter(charWriter))
            {
                writer.Write(new _BufferWriterByte { Foo = "hello", Bar = "world" });
            }

            var str = new string(charWriter.Data.ToArray());

            Assert.Equal("Foo,Bar\r\nhello,world", str);
        }

        private sealed class _FailingGetter
        {
            public int Foo { get; set; }
        }

        [Fact]
        public void FailingGetter()
        {
            var m = ManualTypeDescriber.CreateBuilder(ManualTypeDescriberFallbackBehavior.UseFallback);
            var t = typeof(_FailingGetter).GetTypeInfo();
            var g = Getter.ForMethod(t.GetProperty(nameof(_FailingGetter.Foo)).GetMethod);
            var f = Formatter.ForDelegate((int value, in WriteContext context, IBufferWriter<char> buffer) => false);

            m.WithInstanceProvider(InstanceProvider.ForDelegate((in ReadContext _, out _FailingGetter val) => { val = new _FailingGetter(); return true; }));
            m.WithExplicitGetter(t, "bar", g, f);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(m.ToManualTypeDescriber()).ToOptions();

            RunSyncWriterVariants<_FailingGetter>(
                opts,
                (config, getWriter, getStr) =>
                {
                    using (var w = getWriter())
                    using (var csv = config.CreateWriter(w))
                    {
                        Assert.Throws<SerializationException>(() => csv.Write(new _FailingGetter()));
                    }

                    var res = getStr();
                    Assert.Equal("bar\r\n", res);
                }
            );
        }

        private class _SerializableMemberDefaults
        {
            public int Prop { get; set; }
#pragma warning disable CS0649
            public string Field;
#pragma warning restore CS0649
        }

        [Fact]
        public void SerializableMemberHelpers()
        {
            // fields
            {
                var f = typeof(_SerializableMemberDefaults).GetField(nameof(_SerializableMemberDefaults.Field));

                // 1
                {
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForField(null));

                    var s1 = SerializableMember.ForField(f);
                    Assert.True(s1.EmitDefaultValue);
                    Assert.Equal(Formatter.GetDefault(typeof(string).GetTypeInfo()), s1.Formatter);
                    Assert.Equal(Getter.ForField(f), s1.Getter);
                    Assert.Equal("Field", s1.Name);
                    Assert.False(s1.ShouldSerialize.HasValue);
                }

                // 2
                {
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForField(null, "Nope"));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForField(f, null));

                    var s2 = SerializableMember.ForField(f, "Nope");
                    Assert.True(s2.EmitDefaultValue);
                    Assert.Equal(Formatter.GetDefault(typeof(string).GetTypeInfo()), s2.Formatter);
                    Assert.Equal(Getter.ForField(f), s2.Getter);
                    Assert.Equal("Nope", s2.Name);
                    Assert.False(s2.ShouldSerialize.HasValue);
                }

                var formatter =
                    Formatter.ForDelegate(
                        (string val, in WriteContext ctx, IBufferWriter<char> buffer) =>
                        {
                            return true;
                        }
                    );

                // 3
                {
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForField(null, "Yep", formatter));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForField(f, null, formatter));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForField(f, "Yep", null));

                    var s3 = SerializableMember.ForField(f, "Yep", formatter);
                    Assert.True(s3.EmitDefaultValue);
                    Assert.Equal(formatter, s3.Formatter);
                    Assert.Equal(Getter.ForField(f), s3.Getter);
                    Assert.Equal("Yep", s3.Name);
                    Assert.False(s3.ShouldSerialize.HasValue);
                }

                var shouldSerialize =
                    Cesil.ShouldSerialize.ForDelegate(
                        (in WriteContext _) => true
                    );

                // 4
                {
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForField(null, "Yep", formatter, shouldSerialize));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForField(f, null, formatter, shouldSerialize));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForField(f, "Yep", null, shouldSerialize));
                    // it's ok if shouldSerialize == null

                    var s4 = SerializableMember.ForField(f, "Fizz", formatter, shouldSerialize);
                    Assert.True(s4.EmitDefaultValue);
                    Assert.Equal(formatter, s4.Formatter);
                    Assert.Equal(Getter.ForField(f), s4.Getter);
                    Assert.Equal("Fizz", s4.Name);
                    Assert.Equal(shouldSerialize, s4.ShouldSerialize.Value);
                }

                // 5
                {
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForField(null, "Yep", formatter, shouldSerialize, Cesil.EmitDefaultValue.Yes));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForField(f, null, formatter, shouldSerialize, Cesil.EmitDefaultValue.Yes));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForField(f, "Yep", null, shouldSerialize, Cesil.EmitDefaultValue.Yes));
                    // it's ok if shouldSerialize == null
                    // bad values for WillEmitDefaultValue are tested elsewhere

                    var s5 = SerializableMember.ForField(f, "Buzz", formatter, shouldSerialize, Cesil.EmitDefaultValue.No);
                    Assert.False(s5.EmitDefaultValue);
                    Assert.Equal(formatter, s5.Formatter);
                    Assert.Equal(Getter.ForField(f), s5.Getter);
                    Assert.Equal("Buzz", s5.Name);
                    Assert.Equal(shouldSerialize, s5.ShouldSerialize.Value);
                }
            }

            // property
            {
                var p = typeof(_SerializableMemberDefaults).GetProperty(nameof(_SerializableMemberDefaults.Prop));

                // 1
                {
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForProperty(null));

                    var s1 = SerializableMember.ForProperty(p);
                    Assert.True(s1.EmitDefaultValue);
                    Assert.Equal(Formatter.GetDefault(typeof(int).GetTypeInfo()), s1.Formatter);
                    Assert.Equal((Getter)p.GetMethod, s1.Getter);
                    Assert.Equal("Prop", s1.Name);
                    Assert.False(s1.ShouldSerialize.HasValue);
                }

                // 2
                {
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForProperty(null, "Hello"));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForProperty(p, null));

                    var s2 = SerializableMember.ForProperty(p, "Hello");
                    Assert.True(s2.EmitDefaultValue);
                    Assert.Equal(Formatter.GetDefault(typeof(int).GetTypeInfo()), s2.Formatter);
                    Assert.Equal((Getter)p.GetMethod, s2.Getter);
                    Assert.Equal("Hello", s2.Name);
                    Assert.False(s2.ShouldSerialize.HasValue);
                }

                var formatter =
                    Formatter.ForDelegate(
                        (int val, in WriteContext ctx, IBufferWriter<char> buffer) =>
                        {
                            return true;
                        }
                    );

                // 3
                {
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForProperty(null, "World", formatter));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForProperty(p, null, formatter));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForProperty(p, "World", null));

                    var s3 = SerializableMember.ForProperty(p, "World", formatter);
                    Assert.True(s3.EmitDefaultValue);
                    Assert.Equal(formatter, s3.Formatter);
                    Assert.Equal((Getter)p.GetMethod, s3.Getter);
                    Assert.Equal("World", s3.Name);
                    Assert.False(s3.ShouldSerialize.HasValue);
                }

                var shouldSerialize =
                    Cesil.ShouldSerialize.ForDelegate(
                        (in WriteContext _) => true
                    );

                // 4
                {
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForProperty(null, "Blogo", formatter, shouldSerialize));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForProperty(p, null, formatter, shouldSerialize));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForProperty(p, "Blogo", null, shouldSerialize));
                    // it's ok if shouldSerialize == null

                    var s4 = SerializableMember.ForProperty(p, "Blogo", formatter, shouldSerialize);
                    Assert.True(s4.EmitDefaultValue);
                    Assert.Equal(formatter, s4.Formatter);
                    Assert.Equal((Getter)p.GetMethod, s4.Getter);
                    Assert.Equal("Blogo", s4.Name);
                    Assert.Equal(shouldSerialize, s4.ShouldSerialize.Value);
                }

                // 5
                {
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForProperty(null, "Blogo", formatter, shouldSerialize, Cesil.EmitDefaultValue.Yes));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForProperty(p, null, formatter, shouldSerialize, Cesil.EmitDefaultValue.Yes));
                    Assert.Throws<ArgumentNullException>(() => SerializableMember.ForProperty(p, "Blogo", null, shouldSerialize, Cesil.EmitDefaultValue.Yes));
                    // it's ok if shouldSerialize == null
                    // bad values for WillEmitDefaultValue are tested elsewhere

                    var s5 = SerializableMember.ForProperty(p, "Sphere", formatter, shouldSerialize, Cesil.EmitDefaultValue.No);
                    Assert.False(s5.EmitDefaultValue);
                    Assert.Equal(formatter, s5.Formatter);
                    Assert.Equal((Getter)p.GetMethod, s5.Getter);
                    Assert.Equal("Sphere", s5.Name);
                    Assert.Equal(shouldSerialize, s5.ShouldSerialize.Value);
                }
            }
        }

        [Fact]
        public void SerializableMemberEquality()
        {
            var t = typeof(WriterTests).GetTypeInfo();

            var emitDefaults = new[] { Cesil.EmitDefaultValue.Yes, Cesil.EmitDefaultValue.No };
            IEnumerable<Formatter> formatters;
            {
                var a = (Formatter)(FormatterDelegate<int>)_Formatter;
                var b = Formatter.GetDefault(typeof(int).GetTypeInfo());

                formatters = new[] { a, b };
            }
            IEnumerable<Getter> getters;
            {
                var a = (Getter)(StaticGetterDelegate<int>)((in WriteContext _) => 1);
                var b = (Getter)(StaticGetterDelegate<int>)((in WriteContext _) => 2);

                getters = new[] { a, b };
            }
            var names = new[] { "foo", "bar" };
            IEnumerable<Cesil.ShouldSerialize> shouldSerializes;
            {
                var a = (Cesil.ShouldSerialize)(StaticShouldSerializeDelegate)((in WriteContext _) => true);
                var b = (Cesil.ShouldSerialize)(StaticShouldSerializeDelegate)((in WriteContext _) => false);
                shouldSerializes = new[] { a, b, null };
            }

            var members = new List<SerializableMember>();
            foreach (var e in emitDefaults)
            {
                foreach (var f in formatters)
                {
                    foreach (var g in getters)
                    {
                        foreach (var n in names)
                        {
                            foreach (var s in shouldSerializes)
                            {
                                members.Add(SerializableMember.Create(t, n, g, f, s, e));
                            }
                        }
                    }
                }
            }

            var notSerializableMember = "";

            for (var i = 0; i < members.Count; i++)
            {
                var m1 = members[i];

                Assert.False(m1.Equals(notSerializableMember));

                for (var j = i; j < members.Count; j++)
                {

                    var m2 = members[j];

                    var eq = m1 == m2;
                    var neq = m1 != m2;
                    var hashEq = m1.GetHashCode() == m2.GetHashCode();
                    var objEq = m1.Equals((object)m2);

                    if (i == j)
                    {
                        Assert.True(eq);
                        Assert.False(neq);
                        Assert.True(hashEq);
                        Assert.True(objEq);
                    }
                    else
                    {
                        Assert.False(eq);
                        Assert.True(neq);
                        Assert.False(objEq);
                    }
                }
            }

            static bool _Formatter(int v, in WriteContext wc, IBufferWriter<char> b)
            {
                return true;
            }
        }

        private class _SerializableMemberErrors
        {
#pragma warning disable CS0649
            public int A;
#pragma warning restore CS0649
        }

        private class _SerializeMemberErrors_Unreleated
        {
            public bool ShouldSerializeA() { return true; }
        }

        [Fact]
        public void SerializableMemberErrors()
        {
            var type = typeof(_SerializableMemberErrors).GetTypeInfo();
            Assert.NotNull(type);
            var getter = Getter.ForField(typeof(_SerializableMemberErrors).GetField(nameof(_SerializableMemberErrors.A)));
            Assert.NotNull(getter);
            var formatter = Formatter.GetDefault(typeof(int).GetTypeInfo());
            Assert.NotNull(formatter);

            Assert.Throws<ArgumentNullException>(() => SerializableMember.Create(null, "foo", getter, formatter, null, Cesil.EmitDefaultValue.Yes));
            Assert.Throws<ArgumentNullException>(() => SerializableMember.Create(type, null, getter, formatter, null, Cesil.EmitDefaultValue.Yes));
            Assert.Throws<ArgumentNullException>(() => SerializableMember.Create(type, "foo", null, formatter, null, Cesil.EmitDefaultValue.Yes));
            Assert.Throws<ArgumentNullException>(() => SerializableMember.Create(type, "foo", getter, null, null, Cesil.EmitDefaultValue.Yes));
            Assert.Throws<InvalidOperationException>(() => SerializableMember.Create(type, "foo", getter, formatter, null, 0));

            var shouldSerialize = (ShouldSerialize)typeof(_SerializeMemberErrors_Unreleated).GetMethod(nameof(_SerializeMemberErrors_Unreleated.ShouldSerializeA));
            Assert.NotNull(shouldSerialize);

            Assert.Throws<ArgumentException>(() => SerializableMember.Create(type, "foo", getter, formatter, shouldSerialize, Cesil.EmitDefaultValue.Yes));
        }

        private class _LotsOfComments
        {
            public string Hello { get; set; }
        }

        [Fact]
        public void LotsOfComments()
        {
            var opts = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Always).ToOptions();

            RunSyncWriterVariants<_LotsOfComments>(
                opts,
                (config, getWriter, getStr) =>
                {
                    var cs = string.Join("\r\n", Enumerable.Repeat("foo", 1_000));

                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.WriteComment(cs);
                    }

                    var str = getStr();
                    var expected = nameof(_LotsOfComments.Hello) + "\r\n" + string.Join("\r\n", Enumerable.Repeat("#foo", 1_000));
                    Assert.Equal(expected, str);
                }
            );
        }

        private class _NullCommentError
        {
            public string Foo { get; set; }
        }

        [Fact]
        public void NullCommentError()
        {
            RunSyncWriterVariants<_NullCommentError>(
                Options.Default,
                (config, getWriter, getStr) =>
                {
                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        Assert.Throws<ArgumentNullException>(() => csv.WriteComment(null));
                    }

                    var _ = getStr();
                    Assert.NotNull(_);
                }
            );
        }

        [Fact]
        public void WriteContexts()
        {
            var dc1 = Cesil.WriteContext.DiscoveringCells(Options.Default, 1, null);
            var dc2 = Cesil.WriteContext.DiscoveringCells(Options.Default, 1, "foo");
            var dc3 = Cesil.WriteContext.DiscoveringCells(Options.DynamicDefault, 1, null);

            Assert.Equal(WriteContextMode.DiscoveringCells, dc1.Mode);
            Assert.False(dc1.HasColumn);
            Assert.True(dc1.HasRowNumber);
            Assert.Equal(1, dc1.RowNumber);
            Assert.Throws<InvalidOperationException>(() => dc1.Column);

            var dcol1 = Cesil.WriteContext.DiscoveringColumns(Options.Default, null);
            var dcol2 = Cesil.WriteContext.DiscoveringColumns(Options.Default, "foo");
            var dcol3 = Cesil.WriteContext.DiscoveringColumns(Options.DynamicDefault, null);
            Assert.Equal(WriteContextMode.DiscoveringColumns, dcol1.Mode);
            Assert.False(dcol1.HasRowNumber);
            Assert.False(dcol1.HasColumn);
            Assert.Throws<InvalidOperationException>(() => dcol1.RowNumber);
            Assert.Throws<InvalidOperationException>(() => dcol1.Column);

            var wc1 = Cesil.WriteContext.WritingColumn(Options.Default, 1, ColumnIdentifier.Create(1), null);
            var wc2 = Cesil.WriteContext.WritingColumn(Options.Default, 1, ColumnIdentifier.Create(1), "foo");
            var wc3 = Cesil.WriteContext.WritingColumn(Options.Default, 1, ColumnIdentifier.Create(2), null);
            var wc4 = Cesil.WriteContext.WritingColumn(Options.Default, 2, ColumnIdentifier.Create(1), null);
            var wc5 = Cesil.WriteContext.WritingColumn(Options.DynamicDefault, 1, ColumnIdentifier.Create(1), null);
            Assert.Equal(WriteContextMode.WritingColumn, wc1.Mode);
            Assert.True(wc1.HasColumn);
            Assert.True(wc1.HasRowNumber);

            var contexts = new[] { dc1, dc2, dc3, dcol1, dcol2, dcol3, wc1, wc2, wc3, wc4, wc5 };

            var notContext = "";

            for (var i = 0; i < contexts.Length; i++)
            {
                var ctx1 = contexts[i];
                Assert.False(ctx1.Equals(notContext));
                Assert.NotNull(ctx1.ToString());

                for (var j = i; j < contexts.Length; j++)
                {
                    var ctx2 = contexts[j];

                    var eq = ctx1 == ctx2;
                    var neq = ctx1 != ctx2;
                    var hashEq = ctx1.GetHashCode() == ctx2.GetHashCode();
                    var objEq = ctx1.Equals((object)ctx2);

                    if (i == j)
                    {
                        Assert.True(eq);
                        Assert.True(objEq);
                        Assert.False(neq);
                        Assert.True(hashEq);
                    }
                    else
                    {
                        Assert.False(eq);
                        Assert.False(objEq);
                        Assert.True(neq);
                    }
                }
            }
        }

        private class _WriteComment
        {
            public int Foo { get; set; }
            public int Bar { get; set; }
        }

        [Fact]
        public void WriteComment()
        {
            // no trailing new line
            {
                // first line, no headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    RunSyncWriterVariants<_WriteComment>(
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
                    RunSyncWriterVariants<_WriteComment>(
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
                    RunSyncWriterVariants<_WriteComment>(
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

                // first line, headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Always).ToOptions();

                    // one line
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n#hello", res);
                        }
                    );

                    // two lines
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello\r\nworld");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n#world", res);
                        }
                    );
                }

                // second line, no headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    RunSyncWriterVariants<_WriteComment>(
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
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello");
                            }

                            var res = getStr();
                            Assert.Equal("123,456\r\n#hello", res);
                        }
                    );

                    // two lines
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello\r\nworld");
                            }

                            var res = getStr();
                            Assert.Equal("123,456\r\n#hello\r\n#world", res);
                        }
                    );
                }

                // second line, headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Always).ToOptions();

                    // empty line
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n#", res);
                        }
                    );

                    // one line
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello", res);
                        }
                    );

                    // two lines
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello\r\nworld");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello\r\n#world", res);
                        }
                    );
                }

                // before row, no headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    RunSyncWriterVariants<_WriteComment>(
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
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello");
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = getStr();
                            Assert.Equal("#hello\r\n123,456", res);
                        }
                    );

                    // two lines
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello\r\nworld");
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = getStr();
                            Assert.Equal("#hello\r\n#world\r\n123,456", res);
                        }
                    );
                }

                // before row, headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Always).ToOptions();

                    // empty line
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n#", res);
                        }
                    );

                    // one line
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello");
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n123,456", res);
                        }
                    );

                    // two lines
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello\r\nworld");
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n#world\r\n123,456", res);
                        }
                    );
                }
            }

            // trailing new line
            {
                // first line, no headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    RunSyncWriterVariants<_WriteComment>(
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
                    RunSyncWriterVariants<_WriteComment>(
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
                    RunSyncWriterVariants<_WriteComment>(
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

                // first line, headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Always).ToOptions();

                    // empty line
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n#\r\n", res);
                        }
                    );

                    // one line
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n", res);
                        }
                    );

                    // two lines
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello\r\nworld");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n#world\r\n", res);
                        }
                    );
                }

                // second line, no headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // one line
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello");
                            }

                            var res = getStr();
                            Assert.Equal("123,456\r\n#hello\r\n", res);
                        }
                    );

                    // two lines
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello\r\nworld");
                            }

                            var res = getStr();
                            Assert.Equal("123,456\r\n#hello\r\n#world\r\n", res);
                        }
                    );
                }

                // second line, headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Always).ToOptions();

                    // empty line
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n#\r\n", res);
                        }
                    );

                    // one line
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello\r\n", res);
                        }
                    );

                    // two lines
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                                csv.WriteComment("hello\r\nworld");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello\r\n#world\r\n", res);
                        }
                    );
                }

                // before row, no headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    RunSyncWriterVariants<_WriteComment>(
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
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello");
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = getStr();
                            Assert.Equal("#hello\r\n123,456\r\n", res);
                        }
                    );

                    // two lines
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello\r\nworld");
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = getStr();
                            Assert.Equal("#hello\r\n#world\r\n123,456\r\n", res);
                        }
                    );
                }

                // before row, headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Always).ToOptions();

                    // empty line
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("");
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n#\r\n", res);
                        }
                    );

                    // one line
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello");
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n123,456\r\n", res);
                        }
                    );

                    // two lines
                    RunSyncWriterVariants<_WriteComment>(
                        opts,
                        (config, getWriter, getStr) =>
                        {
                            using (var writer = getWriter())
                            using (var csv = config.CreateWriter(writer))
                            {
                                csv.WriteComment("hello\r\nworld");
                                csv.Write(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n#world\r\n123,456\r\n", res);
                        }
                    );
                }
            }
        }

        private class _DelegateShouldSerialize
        {
            public int Foo { get; set; }
        }

        [Fact]
        public void DelegateStaticShouldSerialize()
        {
            var shouldSerializeCalled = 0;
            StaticShouldSerializeDelegate shouldSerializeDel =
                (in WriteContext _) =>
                {
                    shouldSerializeCalled++;

                    return false;
                };

            var name = nameof(_DelegateShouldSerialize.Foo);
            var getter = (Getter)typeof(_DelegateShouldSerialize).GetProperty(nameof(_DelegateShouldSerialize.Foo)).GetMethod;
            var formatter = Formatter.GetDefault(typeof(int).GetTypeInfo());
            var shouldSerialize = Cesil.ShouldSerialize.ForDelegate(shouldSerializeDel);

            var describer = ManualTypeDescriberBuilder.CreateBuilder();
            describer.WithExplicitGetter(typeof(_DelegateShouldSerialize).GetTypeInfo(), name, getter, formatter, shouldSerialize);
            InstanceProviderDelegate<_DelegateShouldSerialize> del = (in ReadContext _, out _DelegateShouldSerialize i) => { i = new _DelegateShouldSerialize(); return true; };
            describer.WithInstanceProvider((InstanceProvider)del);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)describer.ToManualTypeDescriber()).WithWriteHeader(WriteHeader.Always).ToOptions();

            RunSyncWriterVariants<_DelegateShouldSerialize>(
                opts,
                (config, getWriter, getStr) =>
                {
                    shouldSerializeCalled = 0;

                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.Write(new _DelegateShouldSerialize { Foo = 123 });
                        csv.Write(new _DelegateShouldSerialize { Foo = 0 });
                        csv.Write(new _DelegateShouldSerialize { Foo = 456 });
                    }

                    var res = getStr();
                    Assert.Equal("Foo\r\n\r\n\r\n", res);

                    Assert.Equal(3, shouldSerializeCalled);
                }
            );
        }

        [Fact]
        public void DelegateShouldSerialize()
        {
            var shouldSerializeCalled = 0;
            ShouldSerializeDelegate<_DelegateShouldSerialize> shouldSerializeDel =
                (_DelegateShouldSerialize row, in WriteContext _) =>
                {
                    shouldSerializeCalled++;

                    return row.Foo % 2 != 0;
                };

            var name = nameof(_DelegateShouldSerialize.Foo);
            var getter = (Getter)typeof(_DelegateShouldSerialize).GetProperty(nameof(_DelegateShouldSerialize.Foo)).GetMethod;
            var formatter = Formatter.GetDefault(typeof(int).GetTypeInfo());
            var shouldSerialize = Cesil.ShouldSerialize.ForDelegate(shouldSerializeDel);

            var describer = ManualTypeDescriberBuilder.CreateBuilder();
            describer.WithExplicitGetter(typeof(_DelegateShouldSerialize).GetTypeInfo(), name, getter, formatter, shouldSerialize);
            InstanceProviderDelegate<_DelegateShouldSerialize> del = (in ReadContext _, out _DelegateShouldSerialize i) => { i = new _DelegateShouldSerialize(); return true; };
            describer.WithInstanceProvider((InstanceProvider)del);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)describer.ToManualTypeDescriber()).WithWriteHeader(WriteHeader.Always).ToOptions();

            RunSyncWriterVariants<_DelegateShouldSerialize>(
                opts,
                (config, getWriter, getStr) =>
                {
                    shouldSerializeCalled = 0;

                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.Write(new _DelegateShouldSerialize { Foo = 123 });
                        csv.Write(new _DelegateShouldSerialize { Foo = 0 });
                        csv.Write(new _DelegateShouldSerialize { Foo = 456 });
                    }

                    var res = getStr();
                    Assert.Equal("Foo\r\n123\r\n\r\n", res);

                    Assert.Equal(3, shouldSerializeCalled);
                }
            );
        }

        private class _DelegateFormatter
        {
            public int Foo { get; set; }
        }

        [Fact]
        public void DelegateFormatter()
        {
            var formatterCalled = 0;
            FormatterDelegate<int> formatDel =
                (int val, in WriteContext _, IBufferWriter<char> buffer) =>
                {
                    formatterCalled++;

                    var s = val.ToString();

                    buffer.Write(s);
                    buffer.Write(s);

                    return true;
                };

            var name = nameof(_DelegateFormatter.Foo);
            var getter = (Getter)typeof(_DelegateFormatter).GetProperty(nameof(_DelegateFormatter.Foo)).GetMethod;
            var formatter = Formatter.ForDelegate(formatDel);

            var describer = ManualTypeDescriberBuilder.CreateBuilder();
            describer.WithExplicitGetter(typeof(_DelegateFormatter).GetTypeInfo(), name, getter, formatter);
            InstanceProviderDelegate<_DelegateFormatter> del = (in ReadContext _, out _DelegateFormatter i) => { i = new _DelegateFormatter(); return true; };
            describer.WithInstanceProvider((InstanceProvider)del);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)describer.ToManualTypeDescriber()).WithWriteHeader(WriteHeader.Always).ToOptions();

            RunSyncWriterVariants<_DelegateFormatter>(
                opts,
                (config, getWriter, getStr) =>
                {
                    formatterCalled = 0;

                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.Write(new _DelegateFormatter { Foo = 123 });
                        csv.Write(new _DelegateFormatter { Foo = 0 });
                        csv.Write(new _DelegateFormatter { Foo = 456 });
                    }

                    var res = getStr();
                    Assert.Equal("Foo\r\n123123\r\n00\r\n456456", res);

                    Assert.Equal(3, formatterCalled);
                }
            );
        }

        private class _DelegateGetter
        {
            public int Foo { get; set; }
        }

        [Fact]
        public void DelegateStaticGetter()
        {
            var getterCalled = 0;
            StaticGetterDelegate<int> getDel =
                (in WriteContext _) =>
                {
                    getterCalled++;

                    return getterCalled;
                };

            var name = nameof(_DelegateGetter.Foo);
            var getter = Getter.ForDelegate(getDel);

            var describer = ManualTypeDescriberBuilder.CreateBuilder();
            describer.WithExplicitGetter(typeof(_DelegateGetter).GetTypeInfo(), name, getter);
            InstanceProviderDelegate<_DelegateGetter> del = (in ReadContext _, out _DelegateGetter i) => { i = new _DelegateGetter(); return true; };
            describer.WithInstanceProvider((InstanceProvider)del);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)describer.ToManualTypeDescriber()).WithWriteHeader(WriteHeader.Always).ToOptions();

            RunSyncWriterVariants<_DelegateGetter>(
                opts,
                (config, getWriter, getStr) =>
                {
                    getterCalled = 0;

                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.Write(new _DelegateGetter { Foo = 123 });
                        csv.Write(new _DelegateGetter { Foo = 0 });
                        csv.Write(new _DelegateGetter { Foo = 456 });
                    }

                    var res = getStr();
                    Assert.Equal("Foo\r\n1\r\n2\r\n3", res);

                    Assert.Equal(3, getterCalled);
                }
            );
        }

        [Fact]
        public void DelegateGetter()
        {
            var getterCalled = 0;
            GetterDelegate<_DelegateGetter, int> getDel =
                (_DelegateGetter row, in WriteContext _) =>
                {
                    getterCalled++;

                    return row.Foo * 2;
                };

            var name = nameof(_DelegateGetter.Foo);
            var getter = Getter.ForDelegate(getDel);

            var describer = ManualTypeDescriberBuilder.CreateBuilder();
            describer.WithExplicitGetter(typeof(_DelegateGetter).GetTypeInfo(), name, getter);
            InstanceProviderDelegate<_DelegateGetter> del = (in ReadContext _, out _DelegateGetter i) => { i = new _DelegateGetter(); return true; };
            describer.WithInstanceProvider((InstanceProvider)del);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)describer.ToManualTypeDescriber()).WithWriteHeader(WriteHeader.Always).ToOptions();

            RunSyncWriterVariants<_DelegateGetter>(
                opts,
                (config, getWriter, getStr) =>
                {
                    getterCalled = 0;

                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.Write(new _DelegateGetter { Foo = 123 });
                        csv.Write(new _DelegateGetter { Foo = 0 });
                        csv.Write(new _DelegateGetter { Foo = 456 });
                    }

                    var res = getStr();
                    Assert.Equal("Foo\r\n246\r\n0\r\n912", res);

                    Assert.Equal(3, getterCalled);
                }
            );
        }

        private struct _UserDefinedEmitDefaultValue_ValueType
        {
            public int Value { get; set; }
        }

        private struct _UserDefinedEmitDefaultValue_ValueType_Equatable : IEquatable<_UserDefinedEmitDefaultValue_ValueType_Equatable>
        {
            public static int EqualsCallCount = 0;

            public int Value { get; set; }

            public bool Equals(_UserDefinedEmitDefaultValue_ValueType_Equatable other)
            {
                EqualsCallCount++;

                return Value == other.Value;
            }
        }

        private struct _UserDefinedEmitDefaultValue_ValueType_Operator
        {
            public static int OperatorCallCount = 0;

            public int Value { get; set; }

            public static bool operator ==(_UserDefinedEmitDefaultValue_ValueType_Operator a, _UserDefinedEmitDefaultValue_ValueType_Operator b)
            {
                OperatorCallCount++;

                return a.Value == b.Value;
            }

            public static bool operator !=(_UserDefinedEmitDefaultValue_ValueType_Operator a, _UserDefinedEmitDefaultValue_ValueType_Operator b)
            => !(a == b);

            public override bool Equals(object obj)
            {
                if (obj is _UserDefinedEmitDefaultValue_ValueType_Operator o)
                {
                    return this == o;
                }

                return false;
            }

            public override int GetHashCode()
            => Value;
        }

        private class _UserDefinedEmitDefaultValue1
        {
            public string Foo { get; set; }
            [DataMember(EmitDefaultValue = false)]
            public _UserDefinedEmitDefaultValue_ValueType Bar { get; set; }
        }

        private class _UserDefinedEmitDefaultValue2
        {
            public string Foo { get; set; }
            [DataMember(EmitDefaultValue = false)]
            public _UserDefinedEmitDefaultValue_ValueType_Equatable Bar { get; set; }
        }

        private class _UserDefinedEmitDefaultValue3
        {
            public string Foo { get; set; }
            [DataMember(EmitDefaultValue = false)]
            public _UserDefinedEmitDefaultValue_ValueType_Operator Bar { get; set; }
        }

        private class _UserDefinedEmitDefaultValue_TypeDescripter : DefaultTypeDescriber
        {
            public static bool Format_UserDefinedEmitDefaultValue_ValueType(_UserDefinedEmitDefaultValue_ValueType t, in WriteContext _, IBufferWriter<char> writer)
            {
                var asStr = t.Value.ToString();
                writer.Write(asStr.AsSpan());

                return true;
            }

            public static bool Format_UserDefinedEmitDefaultValue_ValueType_Equatable(_UserDefinedEmitDefaultValue_ValueType_Equatable t, in WriteContext _, IBufferWriter<char> writer)
            {
                var asStr = t.Value.ToString();
                writer.Write(asStr.AsSpan());

                return true;
            }

            public static bool Format_UserDefinedEmitDefaultValue_ValueType_Operator(_UserDefinedEmitDefaultValue_ValueType_Operator t, in WriteContext _, IBufferWriter<char> writer)
            {
                var asStr = t.Value.ToString();
                writer.Write(asStr.AsSpan());

                return true;
            }

            protected override bool ShouldDeserialize(TypeInfo forType, PropertyInfo property)
            {
                if (forType == typeof(_UserDefinedEmitDefaultValue1).GetTypeInfo() && property.Name == nameof(_UserDefinedEmitDefaultValue1.Bar))
                {
                    return false;
                }

                if (forType == typeof(_UserDefinedEmitDefaultValue2).GetTypeInfo() && property.Name == nameof(_UserDefinedEmitDefaultValue2.Bar))
                {
                    return false;
                }

                if (forType == typeof(_UserDefinedEmitDefaultValue3).GetTypeInfo() && property.Name == nameof(_UserDefinedEmitDefaultValue3.Bar))
                {
                    return false;
                }

                return base.ShouldDeserialize(forType, property);
            }

            protected override Formatter GetFormatter(TypeInfo forType, PropertyInfo property)
            {
                if (forType == typeof(_UserDefinedEmitDefaultValue1).GetTypeInfo() && property.Name == nameof(_UserDefinedEmitDefaultValue1.Bar))
                {
                    return (Formatter)typeof(_UserDefinedEmitDefaultValue_TypeDescripter).GetMethod(nameof(Format_UserDefinedEmitDefaultValue_ValueType), BindingFlags.Public | BindingFlags.Static);
                }

                if (forType == typeof(_UserDefinedEmitDefaultValue2).GetTypeInfo() && property.Name == nameof(_UserDefinedEmitDefaultValue2.Bar))
                {
                    return (Formatter)typeof(_UserDefinedEmitDefaultValue_TypeDescripter).GetMethod(nameof(Format_UserDefinedEmitDefaultValue_ValueType_Equatable), BindingFlags.Public | BindingFlags.Static);
                }

                if (forType == typeof(_UserDefinedEmitDefaultValue3).GetTypeInfo() && property.Name == nameof(_UserDefinedEmitDefaultValue3.Bar))
                {
                    return (Formatter)typeof(_UserDefinedEmitDefaultValue_TypeDescripter).GetMethod(nameof(Format_UserDefinedEmitDefaultValue_ValueType_Operator), BindingFlags.Public | BindingFlags.Static);
                }

                return base.GetFormatter(forType, property);
            }
        }

        [Fact]
        public void UserDefinedEmitDefaultValue()
        {
            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(new _UserDefinedEmitDefaultValue_TypeDescripter()).ToOptions();

            // not equatable
            RunSyncWriterVariants<_UserDefinedEmitDefaultValue1>(
                opts,
                (config, getWriter, getStr) =>
                {
                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.Write(new _UserDefinedEmitDefaultValue1 { Foo = "hello", Bar = default });
                        csv.Write(new _UserDefinedEmitDefaultValue1 { Foo = "world", Bar = new _UserDefinedEmitDefaultValue_ValueType { Value = 2 } });
                    }

                    var res = getStr();
                    Assert.Equal("Foo,Bar\r\nhello,\r\nworld,2", res);
                }
            );

            // equatable
            RunSyncWriterVariants<_UserDefinedEmitDefaultValue2>(
                opts,
                (config, getWriter, getStr) =>
                {
                    _UserDefinedEmitDefaultValue_ValueType_Equatable.EqualsCallCount = 0;

                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.Write(new _UserDefinedEmitDefaultValue2 { Foo = "hello", Bar = default });
                        csv.Write(new _UserDefinedEmitDefaultValue2 { Foo = "world", Bar = new _UserDefinedEmitDefaultValue_ValueType_Equatable { Value = 2 } });
                    }

                    var res = getStr();
                    Assert.Equal("Foo,Bar\r\nhello,\r\nworld,2", res);
                    Assert.Equal(2, _UserDefinedEmitDefaultValue_ValueType_Equatable.EqualsCallCount);
                }
            );

            // operator
            RunSyncWriterVariants<_UserDefinedEmitDefaultValue3>(
                opts,
                (config, getWriter, getStr) =>
                {
                    _UserDefinedEmitDefaultValue_ValueType_Operator.OperatorCallCount = 0;

                    using (var writer = getWriter())
                    using (var csv = config.CreateWriter(writer))
                    {
                        csv.Write(new _UserDefinedEmitDefaultValue3 { Foo = "hello", Bar = default });
                        csv.Write(new _UserDefinedEmitDefaultValue3 { Foo = "world", Bar = new _UserDefinedEmitDefaultValue_ValueType_Operator { Value = 2 } });
                    }

                    var res = getStr();
                    Assert.Equal("Foo,Bar\r\nhello,\r\nworld,2", res);
                    Assert.Equal(2, _UserDefinedEmitDefaultValue_ValueType_Operator.OperatorCallCount);
                }
            );
        }

        private class _Context
        {
            [DataMember(Order = 1)]
            public string Foo { get; set; }
            [DataMember(Order = 2)]
            public int Bar { get; set; }
        }

        private static List<string> _Context_FormatFoo_Records;
        public static bool _Context_FormatFoo(string data, in WriteContext ctx, IBufferWriter<char> writer)
        {
            _Context_FormatFoo_Records.Add($"{ctx.RowNumber},{ctx.Column.Name},{ctx.Column.Index},{data},{ctx.Context}");

            var span = data.AsSpan();

            while (!span.IsEmpty)
            {
                var writeTo = writer.GetSpan(span.Length);
                var len = Math.Min(span.Length, writeTo.Length);

                span.Slice(0, len).CopyTo(writeTo);
                writer.Advance(len);

                span = span.Slice(len);
            }

            return true;
        }

        private static List<string> _Context_FormatBar_Records;
        public static bool _Context_FormatBar(int data, in WriteContext ctx, IBufferWriter<char> writer)
        {
            _Context_FormatBar_Records.Add($"{ctx.RowNumber},{ctx.Column.Name},{ctx.Column.Index},{data},{ctx.Context}");

            var asStr = data.ToString();
            writer.Write(asStr.AsSpan());

            return true;
        }

        [Fact]
        public void Context()
        {
            var formatFoo = (Formatter)typeof(WriterTests).GetMethod(nameof(_Context_FormatFoo));
            var formatBar = (Formatter)typeof(WriterTests).GetMethod(nameof(_Context_FormatBar));

            var describer = ManualTypeDescriber.CreateBuilder(ManualTypeDescriberFallbackBehavior.UseFallback);
            describer.WithInstanceProvider((InstanceProvider)typeof(_Context).GetConstructor(Type.EmptyTypes));
            describer.WithSerializableProperty(typeof(_Context).GetProperty(nameof(_Context.Foo)), nameof(_Context.Foo), formatFoo);
            describer.WithSerializableProperty(typeof(_Context).GetProperty(nameof(_Context.Bar)), nameof(_Context.Bar), formatBar);

            var optsBase = Options.CreateBuilder(Options.Default).WithTypeDescriber(describer.ToManualTypeDescriber());

            // no headers
            {
                var opts = optsBase.WithWriteHeader(WriteHeader.Never).ToOptions();

                RunSyncWriterVariants<_Context>(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        _Context_FormatFoo_Records = new List<string>();
                        _Context_FormatBar_Records = new List<string>();

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer, "context!"))
                        {
                            csv.Write(new _Context { Bar = 123, Foo = "whatever" });
                            csv.Write(new _Context { Bar = 456, Foo = "indeed" });
                        }

                        var res = getStr();
                        Assert.Equal("whatever,123\r\nindeed,456", res);

                        Assert.Collection(
                            _Context_FormatFoo_Records,
                            c => Assert.Equal("0,Foo,0,whatever,context!", c),
                            c => Assert.Equal("1,Foo,0,indeed,context!", c)
                        );

                        Assert.Collection(
                            _Context_FormatBar_Records,
                            c => Assert.Equal("0,Bar,1,123,context!", c),
                            c => Assert.Equal("1,Bar,1,456,context!", c)
                        );
                    }
                );
            }

            // with headers
            {
                var opts = optsBase.WithWriteHeader(WriteHeader.Always).ToOptions();

                RunSyncWriterVariants<_Context>(
                    opts,
                    (config, getWriter, getStr) =>
                    {
                        _Context_FormatFoo_Records = new List<string>();
                        _Context_FormatBar_Records = new List<string>();

                        using (var writer = getWriter())
                        using (var csv = config.CreateWriter(writer, "context!"))
                        {
                            csv.Write(new _Context { Bar = 123, Foo = "whatever" });
                            csv.Write(new _Context { Bar = 456, Foo = "indeed" });
                        }

                        var res = getStr();
                        Assert.Equal("Foo,Bar\r\nwhatever,123\r\nindeed,456", res);

                        Assert.Collection(
                            _Context_FormatFoo_Records,
                            c => Assert.Equal("0,Foo,0,whatever,context!", c),
                            c => Assert.Equal("1,Foo,0,indeed,context!", c)
                        );

                        Assert.Collection(
                            _Context_FormatBar_Records,
                            c => Assert.Equal("0,Bar,1,123,context!", c),
                            c => Assert.Equal("1,Bar,1,456,context!", c)
                        );
                    }
                );
            }
        }

        private class _CommentEscape
        {
            public string A { get; set; }
            public string B { get; set; }
        }

        [Fact]
        public void CommentEscape()
        {
            // \r\n
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Never).WithRowEnding(RowEnding.CarriageReturnLineFeed).WithCommentCharacter('#').WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).ToOptions();

                RunSyncWriterVariants<_CommentEscape>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _CommentEscape { A = "#hello", B = "foo" });
                        }

                        var txt = getString();
                        Assert.Equal("\"#hello\",foo\r\n", txt);
                    }
                );

                RunSyncWriterVariants<_CommentEscape>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _CommentEscape { A = "hello", B = "fo#o" });
                        }

                        var txt = getString();
                        Assert.Equal("hello,\"fo#o\"\r\n", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Never).WithRowEnding(RowEnding.CarriageReturn).WithCommentCharacter('#').WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).ToOptions();

                RunSyncWriterVariants<_CommentEscape>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _CommentEscape { A = "#hello", B = "foo" });
                        }

                        var txt = getString();
                        Assert.Equal("\"#hello\",foo\r", txt);
                    }
                );

                RunSyncWriterVariants<_CommentEscape>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _CommentEscape { A = "hello", B = "fo#o" });
                        }

                        var txt = getString();
                        Assert.Equal("hello,\"fo#o\"\r", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Never).WithRowEnding(RowEnding.LineFeed).WithCommentCharacter('#').WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).ToOptions();

                RunSyncWriterVariants<_CommentEscape>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _CommentEscape { A = "#hello", B = "foo" });
                        }

                        var txt = getString();
                        Assert.Equal("\"#hello\",foo\n", txt);
                    }
                );

                RunSyncWriterVariants<_CommentEscape>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _CommentEscape { A = "hello", B = "fo#o" });
                        }

                        var txt = getString();
                        Assert.Equal("hello,\"fo#o\"\n", txt);
                    }
                );
            }
        }

        private class _Simple
        {
            public string Foo { get; set; }
            public int Bar { get; set; }
            public ulong? Nope { get; set; }
        }

        [Fact]
        public void Simple()
        {
            // \r\n
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Never).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

                RunSyncWriterVariants<_Simple>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _Simple { Foo = "hello", Bar = 123, Nope = 456 });
                            writer.Write(new _Simple { Foo = null, Bar = 789, Nope = null });
                        }

                        var txt = getString();
                        Assert.Equal("hello,123,456\r\n,789,", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Never).WithRowEnding(RowEnding.LineFeed).ToOptions();

                RunSyncWriterVariants<_Simple>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _Simple { Foo = "hello", Bar = 123, Nope = 456 });
                            writer.Write(new _Simple { Foo = null, Bar = 789, Nope = null });
                        }

                        var txt = getString();
                        Assert.Equal("hello,123,456\n,789,", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Never).WithRowEnding(RowEnding.CarriageReturn).ToOptions();

                RunSyncWriterVariants<_Simple>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _Simple { Foo = "hello", Bar = 123, Nope = 456 });
                            writer.Write(new _Simple { Foo = null, Bar = 789, Nope = null });
                        }

                        var txt = getString();
                        Assert.Equal("hello,123,456\r,789,", txt);
                    }
                );
            }
        }

        [Fact]
        public void NeedEscape()
        {
            // \r\n
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Never).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

                RunSyncWriterVariants<_Simple>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _Simple { Foo = "hello,world", Bar = 123, Nope = 456 });
                            writer.Write(new _Simple { Foo = "foo\"bar", Bar = 789, Nope = null });
                            writer.Write(new _Simple { Foo = "fizz\r\nbuzz", Bar = -12, Nope = 34 });
                        }

                        var txt = getString();
                        Assert.Equal("\"hello,world\",123,456\r\n\"foo\"\"bar\",789,\r\n\"fizz\r\nbuzz\",-12,34", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Never).WithRowEnding(RowEnding.CarriageReturn).ToOptions();

                RunSyncWriterVariants<_Simple>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _Simple { Foo = "hello,world", Bar = 123, Nope = 456 });
                            writer.Write(new _Simple { Foo = "foo\"bar", Bar = 789, Nope = null });
                            writer.Write(new _Simple { Foo = "fizz\r\nbuzz", Bar = -12, Nope = 34 });
                        }

                        var txt = getString();
                        Assert.Equal("\"hello,world\",123,456\r\"foo\"\"bar\",789,\r\"fizz\r\nbuzz\",-12,34", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Never).WithRowEnding(RowEnding.LineFeed).ToOptions();

                RunSyncWriterVariants<_Simple>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _Simple { Foo = "hello,world", Bar = 123, Nope = 456 });
                            writer.Write(new _Simple { Foo = "foo\"bar", Bar = 789, Nope = null });
                            writer.Write(new _Simple { Foo = "fizz\r\nbuzz", Bar = -12, Nope = 34 });
                        }

                        var txt = getString();
                        Assert.Equal("\"hello,world\",123,456\n\"foo\"\"bar\",789,\n\"fizz\r\nbuzz\",-12,34", txt);
                    }
                );
            }

            // large
            {
                var opts = Options.Default;
                var val = string.Join("", Enumerable.Repeat("abc\r\n", 450));

                RunSyncWriterVariants<_Simple>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _Simple { Foo = val, Bar = 001, Nope = 009 });
                        }

                        var txt = getString();
                        Assert.Equal("Foo,Bar,Nope\r\n\"abc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\n\",1,9", txt);
                    }
                );
            }
        }

        private class _WriteAll
        {
            public string Foo { get; set; }
            public int Bar { get; set; }
            public Guid? Fizz { get; set; }
            public DateTimeOffset Buzz { get; set; }
        }

        [Fact]
        public void WriteAll()
        {
            // \r\n
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

                RunSyncWriterVariants<_WriteAll>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.WriteAll(
                                new[]
                                {
                                    new _WriteAll { Bar = 123, Buzz = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = "hello" },
                                    new _WriteAll { Bar = 456, Buzz = new DateTimeOffset(1980, 2, 2, 1, 1, 1, TimeSpan.Zero), Fizz = null, Foo = "hello" },
                                    new _WriteAll { Bar = 789, Buzz = new DateTimeOffset(1990, 3, 3, 2, 2, 2, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = null },
                                    new _WriteAll { Bar = 012, Buzz = new DateTimeOffset(2000, 4, 4, 3, 3, 3, TimeSpan.Zero), Fizz = null, Foo = null }
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
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.CarriageReturn).ToOptions();

                RunSyncWriterVariants<_WriteAll>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.WriteAll(
                                new[]
                                {
                                    new _WriteAll { Bar = 123, Buzz = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = "hello" },
                                    new _WriteAll { Bar = 456, Buzz = new DateTimeOffset(1980, 2, 2, 1, 1, 1, TimeSpan.Zero), Fizz = null, Foo = "hello" },
                                    new _WriteAll { Bar = 789, Buzz = new DateTimeOffset(1990, 3, 3, 2, 2, 2, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = null },
                                    new _WriteAll { Bar = 012, Buzz = new DateTimeOffset(2000, 4, 4, 3, 3, 3, TimeSpan.Zero), Fizz = null, Foo = null }
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
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.LineFeed).ToOptions();

                RunSyncWriterVariants<_WriteAll>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.WriteAll(
                                new[]
                                {
                                    new _WriteAll { Bar = 123, Buzz = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = "hello" },
                                    new _WriteAll { Bar = 456, Buzz = new DateTimeOffset(1980, 2, 2, 1, 1, 1, TimeSpan.Zero), Fizz = null, Foo = "hello" },
                                    new _WriteAll { Bar = 789, Buzz = new DateTimeOffset(1990, 3, 3, 2, 2, 2, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = null },
                                    new _WriteAll { Bar = 012, Buzz = new DateTimeOffset(2000, 4, 4, 3, 3, 3, TimeSpan.Zero), Fizz = null, Foo = null }
                                }
                            );
                        }

                        var txt = getString();
                        Assert.Equal("Foo,Bar,Fizz,Buzz\nhello,123,5dc798f5-6477-4216-8567-9d17c05fa87e,1970-01-01 00:00:00Z\nhello,456,,1980-02-02 01:01:01Z\n,789,5dc798f5-6477-4216-8567-9d17c05fa87e,1990-03-03 02:02:02Z\n,12,,2000-04-04 03:03:03Z", txt);
                    }
                );
            }
        }

        private class _Headers
        {
            public string Foo { get; set; }
            public int Bar { get; set; }
        }

        [Fact]
        public void Headers()
        {
            // \r\n
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

                RunSyncWriterVariants<_Headers>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _Headers { Foo = "hello", Bar = 123 });
                            writer.Write(new _Headers { Foo = "foo", Bar = 789 });
                        }

                        var txt = getString();
                        Assert.Equal("Foo,Bar\r\nhello,123\r\nfoo,789", txt);
                    }
                );

                // empty
                RunSyncWriterVariants<_Headers>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                        }

                        var txt = getString();
                        Assert.Equal("Foo,Bar", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.CarriageReturn).ToOptions();

                RunSyncWriterVariants<_Headers>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _Headers { Foo = "hello", Bar = 123 });
                            writer.Write(new _Headers { Foo = "foo", Bar = 789 });
                        }

                        var txt = getString();
                        Assert.Equal("Foo,Bar\rhello,123\rfoo,789", txt);
                    }
                );

                // empty
                RunSyncWriterVariants<_Headers>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                        }

                        var txt = getString();
                        Assert.Equal("Foo,Bar", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.LineFeed).ToOptions();

                RunSyncWriterVariants<_Headers>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _Headers { Foo = "hello", Bar = 123 });
                            writer.Write(new _Headers { Foo = "foo", Bar = 789 });
                        }

                        var txt = getString();
                        Assert.Equal("Foo,Bar\nhello,123\nfoo,789", txt);
                    }
                );

                // empty
                RunSyncWriterVariants<_Headers>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                        }

                        var txt = getString();
                        Assert.Equal("Foo,Bar", txt);
                    }
                );
            }
        }

        public class _EscapeHeaders
        {
            [DataMember(Name = "hello\r\nworld")]
            public string A { get; set; }

            [DataMember(Name = "foo,bar")]
            public string B { get; set; }

            [DataMember(Name = "yup")]
            public string C { get; set; }
        }

        [Fact]
        public void EscapeHeaders()
        {
            // \r\n
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.CarriageReturnLineFeed).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).ToOptions();

                RunSyncWriterVariants<_EscapeHeaders>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _EscapeHeaders { A = "fizz", B = "buzz", C = "yes" });
                            writer.Write(new _EscapeHeaders { A = "ping", B = "pong", C = "no" });
                        }

                        var txt = getString();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\r\nfizz,buzz,yes\r\nping,pong,no\r\n", txt);
                    }
                );

                // empty
                RunSyncWriterVariants<_EscapeHeaders>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                        }

                        var txt = getString();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\r\n", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.CarriageReturn).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).ToOptions();

                RunSyncWriterVariants<_EscapeHeaders>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _EscapeHeaders { A = "fizz", B = "buzz", C = "yes" });
                            writer.Write(new _EscapeHeaders { A = "ping", B = "pong", C = "no" });
                        }

                        var txt = getString();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\rfizz,buzz,yes\rping,pong,no\r", txt);
                    }
                );

                // empty
                RunSyncWriterVariants<_EscapeHeaders>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                        }

                        var txt = getString();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\r", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.LineFeed).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).ToOptions();

                RunSyncWriterVariants<_EscapeHeaders>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _EscapeHeaders { A = "fizz", B = "buzz", C = "yes" });
                            writer.Write(new _EscapeHeaders { A = "ping", B = "pong", C = "no" });
                        }

                        var txt = getString();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\nfizz,buzz,yes\nping,pong,no\n", txt);
                    }
                );

                // empty
                RunSyncWriterVariants<_EscapeHeaders>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                        }

                        var txt = getString();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\n", txt);
                    }
                );
            }
        }

        private class _EscapeLargeHeaders
        {
            [DataMember(Name = "A,bcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefgh")]
            public string A { get; set; }
            [DataMember(Name = "Ij,klmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnop")]
            public string B { get; set; }
            [DataMember(Name = "Qrs,tuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwx")]
            public string C { get; set; }
            [DataMember(Name = "0123,4567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567")]
            public string D { get; set; }
            [DataMember(Name = ",,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,")]
            public string E { get; set; }
            [DataMember(Name = "hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world hello\"world")]
            public string F { get; set; }
            [DataMember(Name = "foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar\"foo,bar")]
            public string G { get; set; }
            [DataMember(Name = "fizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz")]
            public string H { get; set; }
        }

        [Fact]
        public void EscapeLargeHeaders()
        {
            // \r\n
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.CarriageReturnLineFeed).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).ToOptions();

                RunSyncWriterVariants<_EscapeLargeHeaders>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _EscapeLargeHeaders { A = "a", B = "b", C = "c", D = "d", E = "e", F = "f", G = "g", H = "h" });
                        }

                        var txt = getString();
                        Assert.Equal("\"A,bcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefgh\",\"Ij,klmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnop\",\"Qrs,tuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwx\",\"0123,4567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567\",\",,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\",\"hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world\",\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\",\"fizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\"\r\na,b,c,d,e,f,g,h\r\n", txt);
                    }
                );

                // empty
                RunSyncWriterVariants<_EscapeLargeHeaders>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                        }

                        var txt = getString();
                        Assert.Equal("\"A,bcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefgh\",\"Ij,klmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnop\",\"Qrs,tuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwx\",\"0123,4567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567\",\",,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\",\"hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world\",\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\",\"fizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\"\r\n", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.CarriageReturn).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).ToOptions();

                RunSyncWriterVariants<_EscapeLargeHeaders>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _EscapeLargeHeaders { A = "a", B = "b", C = "c", D = "d", E = "e", F = "f", G = "g", H = "h" });
                        }

                        var txt = getString();
                        Assert.Equal("\"A,bcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefgh\",\"Ij,klmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnop\",\"Qrs,tuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwx\",\"0123,4567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567\",\",,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\",\"hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world\",\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\",\"fizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\"\ra,b,c,d,e,f,g,h\r", txt);
                    }
                );

                // empty
                RunSyncWriterVariants<_EscapeLargeHeaders>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                        }

                        var txt = getString();
                        Assert.Equal("\"A,bcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefgh\",\"Ij,klmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnop\",\"Qrs,tuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwx\",\"0123,4567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567\",\",,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\",\"hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world\",\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\",\"fizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\"\r", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.LineFeed).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).ToOptions();

                RunSyncWriterVariants<_EscapeLargeHeaders>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                            writer.Write(new _EscapeLargeHeaders { A = "a", B = "b", C = "c", D = "d", E = "e", F = "f", G = "g", H = "h" });
                        }

                        var txt = getString();
                        Assert.Equal("\"A,bcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefgh\",\"Ij,klmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnop\",\"Qrs,tuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwx\",\"0123,4567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567\",\",,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\",\"hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world\",\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\",\"fizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\"\na,b,c,d,e,f,g,h\n", txt);
                    }
                );

                // empty
                RunSyncWriterVariants<_EscapeLargeHeaders>(
                    opts,
                    (config, getWriter, getString) =>
                    {
                        using (var writer = config.CreateWriter(getWriter()))
                        {
                        }

                        var txt = getString();
                        Assert.Equal("\"A,bcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefgh\",\"Ij,klmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnop\",\"Qrs,tuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwx\",\"0123,4567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567\",\",,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\",\"hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world\",\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\",\"fizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\"\n", txt);
                    }
                );
            }
        }

        private class _MultiSegmentValue_TypeDescriber : DefaultTypeDescriber
        {
            protected override Formatter GetFormatter(TypeInfo forType, PropertyInfo property)
            {
                var ret = typeof(_MultiSegmentValue_TypeDescriber).GetMethod(nameof(TryFormatStringCrazy));

                return (Formatter)ret;
            }

            public static bool TryFormatStringCrazy(string val, in WriteContext ctx, IBufferWriter<char> buffer)
            {
                for (var i = 0; i < val.Length; i++)
                {
                    var charSpan = buffer.GetSpan(1);
                    charSpan[0] = val[i];
                    buffer.Advance(1);
                }

                return true;
            }
        }

        private class _MultiSegmentValue
        {
            public string Foo { get; set; }
        }

        [Fact]
        public void MultiSegmentValue()
        {
            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(new _MultiSegmentValue_TypeDescriber()).ToOptions();

            // no encoding
            RunSyncWriterVariants<_MultiSegmentValue>(
                opts,
                (config, getWriter, getString) =>
                {
                    using (var writer = config.CreateWriter(getWriter()))
                    {
                        var row = new _MultiSegmentValue { Foo = string.Join("", Enumerable.Repeat('c', 10_000)) };
                        writer.Write(row);
                    }

                    var txt = getString();
                    Assert.Equal("Foo\r\n" + string.Join("", Enumerable.Repeat('c', 10_000)), txt);
                }
            );

            // quoted
            RunSyncWriterVariants<_MultiSegmentValue>(
                opts,
                (config, getWriter, getString) =>
                {
                    using (var writer = config.CreateWriter(getWriter()))
                    {
                        var row = new _MultiSegmentValue { Foo = string.Join("", Enumerable.Repeat("d,", 10_000)) };
                        writer.Write(row);
                    }

                    var txt = getString();
                    Assert.Equal("Foo\r\n\"" + string.Join("", Enumerable.Repeat("d,", 10_000)) + "\"", txt);
                }
            );

            // escaped
            RunSyncWriterVariants<_MultiSegmentValue>(
                opts,
                (config, getWriter, getString) =>
                {
                    using (var writer = config.CreateWriter(getWriter()))
                    {
                        var row = new _MultiSegmentValue { Foo = string.Join("", Enumerable.Repeat("foo\"bar", 10_000)) };
                        writer.Write(row);
                    }

                    var txt = getString();
                    Assert.Equal("Foo\r\n\"" + string.Join("", Enumerable.Repeat("foo\"\"bar", 10_000)) + "\"", txt);
                }
            );
        }

        private class _ShouldSerialize
        {
            public static bool OnOff;

            public int Foo { get; set; }
            public string Bar { get; set; }

            public bool ShouldSerializeFoo()
            => Foo % 2 == 0;

            public static bool ShouldSerializeBar()
            => OnOff;

            public static void Reset()
            {
                OnOff = default;
            }
        }

        [Fact]
        public void ShouldSerialize()
        {
            _ShouldSerialize.Reset();

            var opts = Options.Default;

            RunSyncWriterVariants<_ShouldSerialize>(
                opts,
                (config, getWriter, getString) =>
                {
                    _ShouldSerialize.Reset();

                    using (var csv = config.CreateWriter(getWriter()))
                    {
                        csv.Write(new _ShouldSerialize { Foo = 1, Bar = "hello" });
                        _ShouldSerialize.OnOff = !_ShouldSerialize.OnOff;
                        csv.Write(new _ShouldSerialize { Foo = 3, Bar = "world" });
                        _ShouldSerialize.OnOff = !_ShouldSerialize.OnOff;
                        csv.Write(new _ShouldSerialize { Foo = 4, Bar = "fizz" });
                        _ShouldSerialize.OnOff = !_ShouldSerialize.OnOff;
                        csv.Write(new _ShouldSerialize { Foo = 9, Bar = "buzz" });
                        _ShouldSerialize.OnOff = true;
                        csv.Write(new _ShouldSerialize { Foo = 10, Bar = "bonzai" });
                    }

                    var txt = getString();
                    Assert.Equal("Foo,Bar\r\n,\r\n,world\r\n4,\r\n,buzz\r\n10,bonzai", txt);
                }
            );
        }

        private class _VariousGetters
        {
            private int Foo;

            public _VariousGetters() { }

            public _VariousGetters(int f) : this()
            {
                Foo = f;
            }

            public static int GetBar() => 2;

            public static int GetFizz(_VariousGetters sg) => sg.Foo + GetBar();

            public static int GetBuzz(ref WriteContext _) => 3;
            public static int GetHello(_VariousGetters sg, ref WriteContext _) => sg.Foo + 4;
            public int GetWorld(ref WriteContext _) => 5;


        }

        [Fact]
        public void VariousGetters()
        {
            var m = ManualTypeDescriberBuilder.CreateBuilder();
            m.WithInstanceProvider((InstanceProvider)typeof(_VariousGetters).GetConstructor(Type.EmptyTypes));
            m.WithExplicitGetter(typeof(_VariousGetters).GetTypeInfo(), "Bar", (Getter)typeof(_VariousGetters).GetMethod("GetBar", BindingFlags.Static | BindingFlags.Public));
            m.WithExplicitGetter(typeof(_VariousGetters).GetTypeInfo(), "Fizz", (Getter)typeof(_VariousGetters).GetMethod("GetFizz", BindingFlags.Static | BindingFlags.Public));
            m.WithExplicitGetter(typeof(_VariousGetters).GetTypeInfo(), "Buzz", (Getter)typeof(_VariousGetters).GetMethod("GetBuzz", BindingFlags.Static | BindingFlags.Public));
            m.WithExplicitGetter(typeof(_VariousGetters).GetTypeInfo(), "Hello", (Getter)typeof(_VariousGetters).GetMethod("GetHello", BindingFlags.Static | BindingFlags.Public));
            m.WithExplicitGetter(typeof(_VariousGetters).GetTypeInfo(), "World", (Getter)typeof(_VariousGetters).GetMethod("GetWorld", BindingFlags.Instance | BindingFlags.Public));

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)m.ToManualTypeDescriber()).ToOptions();

            RunSyncWriterVariants<_VariousGetters>(
                opts,
                (config, getWriter, getStr) =>
                {
                    using (var csv = config.CreateWriter(getWriter()))
                    {
                        csv.Write(new _VariousGetters(1));
                        csv.Write(new _VariousGetters(2));
                        csv.Write(new _VariousGetters(3));
                    }

                    var str = getStr();
                    Assert.Equal("Bar,Fizz,Buzz,Hello,World\r\n2,3,3,5,5\r\n2,4,3,6,5\r\n2,5,3,7,5", str);
                }
            );
        }

        private class _EmitDefaultValue
        {
            public enum E
            {
                None = 0,
                Fizz,
                Buzz
            }

            [DataMember(EmitDefaultValue = false)]
            public int Foo { get; set; }
            [DataMember(EmitDefaultValue = false)]
            public E Bar { get; set; }
            [DataMember(EmitDefaultValue = false)]
            public E? Hello { get; set; }
            [DataMember(EmitDefaultValue = false)]
            public DateTime World { get; set; }
        }

        [Fact]
        public void EmitDefaultValue()
        {
            var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Never).ToOptions();

            RunSyncWriterVariants<_EmitDefaultValue>(
                opts,
                (config, getWriter, getString) =>
                {
                    using (var writer = config.CreateWriter(getWriter()))
                    {
                        var rows =
                            new[]
                            {
                            new _EmitDefaultValue { Foo = 1, Bar = _EmitDefaultValue.E.None, Hello = _EmitDefaultValue.E.None, World = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)},
                            new _EmitDefaultValue { Foo = 0, Bar = _EmitDefaultValue.E.Fizz, Hello = null, World = default},
                            };

                        writer.WriteAll(rows);
                    }

                    var txt = getString();
                    Assert.Equal("1,,None,1970-01-01 00:00:00Z\r\n,Fizz,,", txt);
                }
            );
        }

        [Fact]
        public async Task ReturnedRowCountsAsync()
        {
            // simple
            await RunAsyncWriterVariants<_ReturnedRowCounts>(
                Options.Default,
                async (config, getWriter, getStr) =>
                {
                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        var n1 =
                            await csv.WriteAllAsync(
                                new[]
                                {
                                    new _ReturnedRowCounts
                                    {
                                        A = "Foo",
                                        B = 123
                                    },
                                    new _ReturnedRowCounts
                                    {
                                        A = "Bar",
                                        B = 456
                                    },
                                    new _ReturnedRowCounts
                                    {
                                        A = "Fizz",
                                        B = 456
                                    }
                                }
                            );

                        Assert.Equal(3, n1);

                        var n2 =
                            await csv.WriteAllAsync(
                                new[]
                                {
                                    new _ReturnedRowCounts
                                    {
                                        A = "Buzz",
                                        B = 789
                                    },
                                }
                            );
                        Assert.Equal(1, n2);
                    }

                    var res = await getStr();
                    Assert.Equal("A,B\r\nFoo,123\r\nBar,456\r\nFizz,456\r\nBuzz,789", res);
                }
            );

            // no count
            await RunAsyncWriterVariants<(string, int)>(
                Options.Default,
                async (config, getWriter, getStr) =>
                {
                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        var n1 =
                            await csv.WriteAllAsync(
                                Enumerable.Range(0, 10).Select(x => (A: x.ToString(), B: x * 2))
                            );

                        Assert.Equal(10, n1);

                        var n2 =
                            await csv.WriteAllAsync(
                               new[] { (A: "nope", B: -1) }
                            );

                        Assert.Equal(1, n2);
                    }

                    var res = await getStr();
                    Assert.Equal("Item1,Item2\r\n0,0\r\n1,2\r\n2,4\r\n3,6\r\n4,8\r\n5,10\r\n6,12\r\n7,14\r\n8,16\r\n9,18\r\nnope,-1", res);
                }
            );

            // no count, async
            await RunAsyncWriterVariants<(string, int)>(
                Options.Default,
                async (config, getWriter, getStr) =>
                {
                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        var enumerable1 = new TestAsyncEnumerable<(string, int)>(Enumerable.Range(0, 10).Select(x => (A: x + "-" + x, B: x * 2)), false);

                        var n1 = await csv.WriteAllAsync(enumerable1);
                        Assert.Equal(10, n1);

                        var enumerable2 = new TestAsyncEnumerable<(string, int)>(new[] { (A: "wat", B: 15) }, false);

                        var n2 = await csv.WriteAllAsync(enumerable2);
                        Assert.Equal(1, n2);
                    }

                    var res = await getStr();
                    Assert.Equal("Item1,Item2\r\n0-0,0\r\n1-1,2\r\n2-2,4\r\n3-3,6\r\n4-4,8\r\n5-5,10\r\n6-6,12\r\n7-7,14\r\n8-8,16\r\n9-9,18\r\nwat,15", res);
                }
            );
        }

        [Fact]
        public async Task NoEscapesMultiCharacterSeparatorAsync()
        {
            // no escapes at all (TSV)
            {
                var opts = OptionsBuilder.CreateBuilder(Options.Default).WithValueSeparator("**").WithEscapedValueStartAndEnd(null).WithEscapedValueEscapeCharacter(null).ToOptions();

                // correct
                await RunAsyncWriterVariants<_NoEscapes>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(new _NoEscapes { Foo = "abc", Bar = "123" });
                            await csv.WriteAsync(new _NoEscapes { Foo = "\"", Bar = "," });
                        }

                        var str = await getStr();
                        Assert.Equal("Foo**Bar\r\nabc**123\r\n\"**,", str);
                    }
                );

                // explodes if there's an value separator in a value, since there are no escapes
                {
                    // \t
                    await RunAsyncWriterVariants<_NoEscapes>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                var inv = await Assert.ThrowsAsync<InvalidOperationException>(async () => await csv.WriteAsync(new _NoEscapes { Foo = "h**lo", Bar = "foo" }));

                                Assert.Equal("Tried to write a value contain '**' which requires escaping a value, but no way to escape a value is configured", inv.Message);
                            }

                            await getStr();
                        }
                    );

                    // \r\n
                    await RunAsyncWriterVariants<_NoEscapes>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                var inv = await Assert.ThrowsAsync<InvalidOperationException>(async () => await csv.WriteAsync(new _NoEscapes { Foo = "foo", Bar = "\r\n" }));

                                Assert.Equal("Tried to write a value contain '\r' which requires escaping a value, but no way to escape a value is configured", inv.Message);
                            }

                            await getStr();
                        }
                    );

                    var optsWithComment = OptionsBuilder.CreateBuilder(opts).WithCommentCharacter('#').ToOptions();
                    // #
                    await RunAsyncWriterVariants<_NoEscapes>(
                        optsWithComment,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                var inv = await Assert.ThrowsAsync<InvalidOperationException>(async () => await csv.WriteAsync(new _NoEscapes { Foo = "#", Bar = "fizz" }));

                                Assert.Equal("Tried to write a value contain '#' which requires escaping a value, but no way to escape a value is configured", inv.Message);
                            }

                            await getStr();
                        }
                    );
                }
            }

            // escapes, but no escape for the escape start and end char
            {
                var opts = OptionsBuilder.CreateBuilder(Options.Default).WithValueSeparator("**").WithEscapedValueStartAndEnd('"').WithEscapedValueEscapeCharacter(null).ToOptions();

                // correct
                await RunAsyncWriterVariants<_NoEscapes>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(new _NoEscapes { Foo = "a**bc", Bar = "#123" });
                            await csv.WriteAsync(new _NoEscapes { Foo = "\r", Bar = "," });
                        }

                        var str = await getStr();
                        Assert.Equal("Foo**Bar\r\n\"a**bc\"**#123\r\n\"\r\"**,", str);
                    }
                );

                var optsWithComments = OptionsBuilder.CreateBuilder(opts).WithCommentCharacter('#').ToOptions();

                // correct with comments
                await RunAsyncWriterVariants<_NoEscapes>(
                    optsWithComments,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(new _NoEscapes { Foo = "a**bc", Bar = "#123" });
                            await csv.WriteAsync(new _NoEscapes { Foo = "\r", Bar = "," });
                        }

                        var str = await getStr();
                        Assert.Equal("Foo**Bar\r\n\"a**bc\"**\"#123\"\r\n\"\r\"**,", str);
                    }
                );

                // explodes if there's an escape start character in a value, since it can't be escaped
                await RunAsyncWriterVariants<_NoEscapes>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            var inv = await Assert.ThrowsAsync<InvalidOperationException>(async () => await csv.WriteAsync(new _NoEscapes { Foo = "a**bc", Bar = "\"" }));

                            Assert.Equal("Tried to write a value contain '\"' which requires escaping the character in an escaped value, but no way to escape inside an escaped value is configured", inv.Message);
                        }

                        await getStr();
                    }
                );
            }
        }

        [Fact]
        public async Task MultiCharacterValueSeparatorsAsync()
        {
            var opts = Options.CreateBuilder(Options.Default).WithValueSeparator("#*#").ToOptions();

            // no escapes
            {
                await RunAsyncWriterVariants<_MultiCharacterValueSeparators>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(new _MultiCharacterValueSeparators { A = 123, B = "foo" });
                            await csv.WriteAsync(new _MultiCharacterValueSeparators { A = 456, B = "#" });
                            await csv.WriteAsync(new _MultiCharacterValueSeparators { A = 789, B = "*" });
                        }

                        var res = await getStr();
                        Assert.Equal("A#*#B\r\n123#*#foo\r\n456#*##\r\n789#*#*", res);
                    }
                );
            }

            // escapes
            {
                await RunAsyncWriterVariants<_MultiCharacterValueSeparators>(
                   opts,
                   async (config, getWriter, getStr) =>
                   {
                       await using (var writer = getWriter())
                       await using (var csv = config.CreateAsyncWriter(writer))
                       {
                           await csv.WriteAsync(new _MultiCharacterValueSeparators { A = 123, B = "foo#*#bar" });
                           await csv.WriteAsync(new _MultiCharacterValueSeparators { A = 456, B = "#" });
                           await csv.WriteAsync(new _MultiCharacterValueSeparators { A = 789, B = "*" });
                       }

                       var res = await getStr();
                       Assert.Equal("A#*#B\r\n123#*#\"foo#*#bar\"\r\n456#*##\r\n789#*#*", res);
                   }
               );
            }

            // in headers
            {
                await RunAsyncWriterVariants<_MultiCharacterValueSeparators_Headers>(
                  opts,
                  async (config, getWriter, getStr) =>
                  {
                      await using (var writer = getWriter())
                      await using (var csv = config.CreateAsyncWriter(writer))
                      {
                          await csv.WriteAsync(new _MultiCharacterValueSeparators_Headers { A = 123, B = "foo#*#bar" });
                          await csv.WriteAsync(new _MultiCharacterValueSeparators_Headers { A = 456, B = "#" });
                          await csv.WriteAsync(new _MultiCharacterValueSeparators_Headers { A = 789, B = "*" });
                      }

                      var res = await getStr();
                      Assert.Equal("\"A#*#Escaped\"#*#B\r\n123#*#\"foo#*#bar\"\r\n456#*##\r\n789#*#*", res);
                  }
              );
            }
        }

        [Fact]
        public async Task WellKnownSingleColumnsAsync()
        {
            // bool
            {
                await RunAsyncWriterVariants<bool>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new[] { true, false, true });
                        }

                        var res = await getStr();
                        Assert.Equal("Boolean\r\nTrue\r\nFalse\r\nTrue", res);
                    }
                );
            }

            // bool?
            {
                await RunAsyncWriterVariants<bool?>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new bool?[] { true, false, null });
                        }

                        var res = await getStr();
                        Assert.Equal("NullableBoolean\r\nTrue\r\nFalse\r\n", res);
                    }
                );
            }

            // char
            {
                await RunAsyncWriterVariants<char>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new[] { 'a', 'b', 'c' });
                        }

                        var res = await getStr();
                        Assert.Equal("Char\r\na\r\nb\r\nc", res);
                    }
                );
            }

            // char?
            {
                await RunAsyncWriterVariants<char?>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new char?[] { 'a', null, 'c' });
                        }

                        var res = await getStr();
                        Assert.Equal("NullableChar\r\na\r\n\r\nc", res);
                    }
                );
            }

            // byte
            {
                await RunAsyncWriterVariants<byte>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new byte[] { 0, 128, 255 });
                        }

                        var res = await getStr();
                        Assert.Equal("Byte\r\n0\r\n128\r\n255", res);
                    }
                );
            }

            // byte?
            {
                await RunAsyncWriterVariants<byte?>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new byte?[] { 0, null, 255 });
                        }

                        var res = await getStr();
                        Assert.Equal("NullableByte\r\n0\r\n\r\n255", res);
                    }
                );
            }

            // sbyte
            {
                await RunAsyncWriterVariants<sbyte>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new sbyte[] { 0, -127, -2 });
                        }

                        var res = await getStr();
                        Assert.Equal("SByte\r\n0\r\n-127\r\n-2", res);
                    }
                );
            }

            // sbyte?
            {
                await RunAsyncWriterVariants<sbyte?>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new sbyte?[] { null, -127, -2 });
                        }

                        var res = await getStr();
                        Assert.Equal("NullableSByte\r\n\r\n-127\r\n-2", res);
                    }
                );
            }

            // short
            {
                await RunAsyncWriterVariants<short>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new short[] { 0, -9876, -16000 });
                        }

                        var res = await getStr();
                        Assert.Equal("Int16\r\n0\r\n-9876\r\n-16000", res);
                    }
                );
            }

            // short?
            {
                await RunAsyncWriterVariants<short?>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new short?[] { 0, null, -16000 });
                        }

                        var res = await getStr();
                        Assert.Equal("NullableInt16\r\n0\r\n\r\n-16000", res);
                    }
                );
            }

            // ushort
            {
                await RunAsyncWriterVariants<ushort>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new ushort[] { 0, 12345, 32000 });
                        }

                        var res = await getStr();
                        Assert.Equal("UInt16\r\n0\r\n12345\r\n32000", res);
                    }
                );
            }

            // ushort?
            {
                await RunAsyncWriterVariants<ushort?>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new ushort?[] { null, 12345, 32000 });
                        }

                        var res = await getStr();
                        Assert.Equal("NullableUInt16\r\n\r\n12345\r\n32000", res);
                    }
                );
            }

            // int
            {
                await RunAsyncWriterVariants<int>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new int[] { 0, 2000000, -15 });
                        }

                        var res = await getStr();
                        Assert.Equal("Int32\r\n0\r\n2000000\r\n-15", res);
                    }
                );
            }

            // int?
            {
                await RunAsyncWriterVariants<int?>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new int?[] { null, 2000000, -15 });
                        }

                        var res = await getStr();
                        Assert.Equal("NullableInt32\r\n\r\n2000000\r\n-15", res);
                    }
                );
            }

            // uint
            {
                await RunAsyncWriterVariants<uint>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new uint[] { 0, 2000000, 4_000_000_000 });
                        }

                        var res = await getStr();
                        Assert.Equal("UInt32\r\n0\r\n2000000\r\n4000000000", res);
                    }
                );
            }

            // uint?
            {
                await RunAsyncWriterVariants<uint?>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new uint?[] { null, 2000000, 4_000_000_000 });
                        }

                        var res = await getStr();
                        Assert.Equal("NullableUInt32\r\n\r\n2000000\r\n4000000000", res);
                    }
                );
            }

            // long
            {
                await RunAsyncWriterVariants<long>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new long[] { 0, long.MinValue, long.MaxValue });
                        }

                        var res = await getStr();
                        Assert.Equal($"Int64\r\n0\r\n{long.MinValue}\r\n{long.MaxValue}", res);
                    }
                );
            }

            // long?
            {
                await RunAsyncWriterVariants<long?>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new long?[] { long.MinValue, null, long.MaxValue });
                        }

                        var res = await getStr();
                        Assert.Equal($"NullableInt64\r\n{long.MinValue}\r\n\r\n{long.MaxValue}", res);
                    }
                );
            }

            // ulong
            {
                await RunAsyncWriterVariants<ulong>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new ulong[] { 0, 123, ulong.MaxValue });
                        }

                        var res = await getStr();
                        Assert.Equal($"UInt64\r\n0\r\n123\r\n{ulong.MaxValue}", res);
                    }
                );
            }

            // ulong?
            {
                await RunAsyncWriterVariants<ulong?>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new ulong?[] { 0, null, ulong.MaxValue });
                        }

                        var res = await getStr();
                        Assert.Equal($"NullableUInt64\r\n0\r\n\r\n{ulong.MaxValue}", res);
                    }
                );
            }

            // float
            {
                await RunAsyncWriterVariants<float>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new float[] { 0.12f, 123456789.0123f, -999999.88888f });
                        }

                        var res = await getStr();
                        Assert.Equal("Single\r\n0.119999997\r\n123456792\r\n-999999.875", res);
                    }
                );
            }

            // float?
            {
                await RunAsyncWriterVariants<float?>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new float?[] { 0.12f, null, -999999.88888f });
                        }

                        var res = await getStr();
                        Assert.Equal($"NullableSingle\r\n0.119999997\r\n\r\n-999999.875", res);
                    }
                );
            }

            // double
            {
                await RunAsyncWriterVariants<double>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new double[] { 0.12, 123456789.0123, -999999.88888 });
                        }

                        var res = await getStr();
                        Assert.Equal("Double\r\n0.12\r\n123456789.0123\r\n-999999.88887999998", res);
                    }
                );
            }

            // double?
            {
                await RunAsyncWriterVariants<double?>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new double?[] { 0.12, null, -999999.88888 });
                        }

                        var res = await getStr();
                        Assert.Equal($"NullableDouble\r\n0.12\r\n\r\n-999999.88887999998", res);
                    }
                );
            }

            // decimal
            {
                await RunAsyncWriterVariants<decimal>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new decimal[] { 0.12m, 123456789.0123m, -999999.88888m });
                        }

                        var res = await getStr();
                        Assert.Equal($"Decimal\r\n0.12\r\n123456789.0123\r\n-999999.88888", res);
                    }
                );
            }

            // decimal?
            {
                await RunAsyncWriterVariants<decimal?>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new decimal?[] { 0.12m, null, -999999.88888m });
                        }

                        var res = await getStr();
                        Assert.Equal($"NullableDecimal\r\n0.12\r\n\r\n-999999.88888", res);
                    }
                );
            }

            // string
            {
                await RunAsyncWriterVariants<string>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new string[] { "hello", null, "world" });
                        }

                        var res = await getStr();
                        Assert.Equal($"String\r\nhello\r\n\r\nworld", res);
                    }
                );
            }

            // Version
            {
                await RunAsyncWriterVariants<Version>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new Version[] { new Version(1, 2), null, new Version(1, 2, 3, 4) });
                        }

                        var res = await getStr();
                        Assert.Equal($"Version\r\n1.2\r\n\r\n1.2.3.4", res);
                    }
                );
            }

            // Uri
            {
                await RunAsyncWriterVariants<Uri>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new Uri[] { new Uri("http://example.com/"), null, new Uri("https://stackoverflow.com/questions") });
                        }

                        var res = await getStr();
                        Assert.Equal($"Uri\r\nhttp://example.com/\r\n\r\nhttps://stackoverflow.com/questions", res);
                    }
                );
            }

            // enum
            {
                await RunAsyncWriterVariants<_WellKnownSingleColumns>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new _WellKnownSingleColumns[] { _WellKnownSingleColumns.Foo, _WellKnownSingleColumns.Bar, _WellKnownSingleColumns.Foo });
                        }

                        var res = await getStr();
                        Assert.Equal($"_WellKnownSingleColumns\r\nFoo\r\nBar\r\nFoo", res);
                    }
                );
            }

            // enum?
            {
                await RunAsyncWriterVariants<_WellKnownSingleColumns?>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new _WellKnownSingleColumns?[] { _WellKnownSingleColumns.Foo, _WellKnownSingleColumns.Bar, null });
                        }

                        var res = await getStr();
                        Assert.Equal($"Nullable_WellKnownSingleColumns\r\nFoo\r\nBar\r\n", res);
                    }
                );
            }

            // flags enum
            {
                await RunAsyncWriterVariants<_WellKnownSingleColumns_Flags>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new _WellKnownSingleColumns_Flags[] { _WellKnownSingleColumns_Flags.Foo | _WellKnownSingleColumns_Flags.Bar, _WellKnownSingleColumns_Flags.Bar, _WellKnownSingleColumns_Flags.Fizz });
                        }

                        var res = await getStr();
                        Assert.Equal($"_WellKnownSingleColumns_Flags\r\n\"Foo, Bar\"\r\nBar\r\nFizz", res);
                    }
                );
            }

            // flags enum?
            {
                await RunAsyncWriterVariants<_WellKnownSingleColumns_Flags?>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(new _WellKnownSingleColumns_Flags?[] { _WellKnownSingleColumns_Flags.Foo | _WellKnownSingleColumns_Flags.Bar, null, _WellKnownSingleColumns_Flags.Fizz });
                        }

                        var res = await getStr();
                        Assert.Equal($"Nullable_WellKnownSingleColumns_Flags\r\n\"Foo, Bar\"\r\n\r\nFizz", res);
                    }
                );
            }

            // DateTime
            {
                await RunAsyncWriterVariants<DateTime>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        var ci = CultureInfo.InvariantCulture;

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(
                                new DateTime[]
                                {
                                    DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc),
                                    new DateTime(2020, 04, 23, 0, 0, 0, DateTimeKind.Utc),
                                    DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc)
                                }
                            );
                        }

                        var res = await getStr();
                        Assert.Equal($"DateTime\r\n{DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc).ToString("u", ci)}\r\n{new DateTime(2020, 04, 23, 0, 0, 0, DateTimeKind.Utc).ToString("u", ci)}\r\n{DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc).ToString("u", ci)}", res);
                    }
                );
            }

            // DateTime?
            {
                await RunAsyncWriterVariants<DateTime?>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        var ci = CultureInfo.InvariantCulture;

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(
                                new DateTime?[]
                                {
                                    DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc),
                                    null,
                                    DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc),
                                }
                            );
                        }

                        var res = await getStr();
                        Assert.Equal($"NullableDateTime\r\n{DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc).ToString("u", ci)}\r\n\r\n{DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc).ToString("u", ci)}", res);
                    }
                );
            }

            // DateTimeOffset
            {
                await RunAsyncWriterVariants<DateTimeOffset>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        var ci = CultureInfo.InvariantCulture;

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(
                                new DateTimeOffset[]
                                {
                                    DateTimeOffset.MaxValue,
                                    new DateTimeOffset(2020, 04, 23, 0, 0, 0, TimeSpan.Zero),
                                    DateTimeOffset.MinValue,
                                }
                            );
                        }

                        var res = await getStr();
                        Assert.Equal($"DateTimeOffset\r\n{DateTimeOffset.MaxValue.ToString("u", ci)}\r\n{new DateTimeOffset(2020, 04, 23, 0, 0, 0, TimeSpan.Zero).ToString("u", ci)}\r\n{DateTimeOffset.MinValue.ToString("u", ci)}", res);
                    }
                );
            }

            // DateTimeOffset?
            {
                await RunAsyncWriterVariants<DateTimeOffset?>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        var ci = CultureInfo.InvariantCulture;

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(
                                new DateTimeOffset?[]
                                {
                                    DateTimeOffset.MaxValue,
                                    null,
                                    DateTimeOffset.MinValue,
                                }
                            );
                        }

                        var res = await getStr();
                        Assert.Equal($"NullableDateTimeOffset\r\n{DateTimeOffset.MaxValue.ToString("u", ci)}\r\n\r\n{DateTimeOffset.MinValue.ToString("u", ci)}", res);
                    }
                );
            }

            // Guid
            {
                await RunAsyncWriterVariants<Guid>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(
                                new Guid[]
                                {
                                    Guid.Parse("2E9348A1-C3D9-4A9C-95FF-D97591F91542"),
                                    Guid.Parse("ECB04C56-3042-4234-B757-6AC6E53E10C2")
                                }
                            );
                        }

                        var res = await getStr();
                        Assert.Equal($"Guid\r\n2e9348a1-c3d9-4a9c-95ff-d97591f91542\r\necb04c56-3042-4234-b757-6ac6e53e10c2", res);
                    }
                );
            }

            // Guid?
            {
                await RunAsyncWriterVariants<Guid?>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(
                                new Guid?[]
                                {
                                    null,
                                    Guid.Parse("ECB04C56-3042-4234-B757-6AC6E53E10C2")
                                }
                            );
                        }

                        var res = await getStr();
                        Assert.Equal($"NullableGuid\r\n\r\necb04c56-3042-4234-b757-6ac6e53e10c2", res);
                    }
                );
            }

            // TimeSpan
            {
                await RunAsyncWriterVariants<TimeSpan>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(
                                new TimeSpan[]
                                {
                                    TimeSpan.MaxValue,
                                    TimeSpan.FromMilliseconds(123456),
                                    TimeSpan.MaxValue
                                }
                            );
                        }

                        var res = await getStr();
                        Assert.Equal($"TimeSpan\r\n{TimeSpan.MaxValue}\r\n{TimeSpan.FromMilliseconds(123456)}\r\n{TimeSpan.MaxValue}", res);
                    }
                );
            }

            // TimeSpan?
            {
                await RunAsyncWriterVariants<TimeSpan?>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(
                                new TimeSpan?[]
                                {
                                    TimeSpan.MaxValue,
                                    null,
                                    TimeSpan.MaxValue
                                }
                            );
                        }

                        var res = await getStr();
                        Assert.Equal($"NullableTimeSpan\r\n{TimeSpan.MaxValue}\r\n\r\n{TimeSpan.MaxValue}", res);
                    }
                );
            }

            // Index
            {
                await RunAsyncWriterVariants<Index>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(
                                new Index[]
                                {
                                    ^1,
                                    (Index)2,
                                    ^3
                                }
                            );
                        }

                        var res = await getStr();
                        Assert.Equal($"Index\r\n{^1}\r\n{(Index)2}\r\n{^3}", res);
                    }
                );
            }

            // Index?
            {
                await RunAsyncWriterVariants<Index?>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(
                                new Index?[]
                                {
                                    ^1,
                                    null,
                                    ^3
                                }
                            );
                        }

                        var res = await getStr();
                        Assert.Equal($"NullableIndex\r\n{^1}\r\n\r\n{^3}", res);
                    }
                );
            }

            // Range
            {
                await RunAsyncWriterVariants<Range>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(
                                new Range[]
                                {
                                    1..^1,
                                    ..^2,
                                    ^3..
                                }
                            );
                        }

                        var res = await getStr();
                        Assert.Equal($"Range\r\n{1..^1}\r\n{..^2}\r\n{^3..}", res);
                    }
                );
            }

            // Range?
            {
                await RunAsyncWriterVariants<Range?>(
                    Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAllAsync(
                                new Range?[]
                                {
                                    1..^1,
                                    null,
                                    ^3..
                                }
                            );
                        }

                        var res = await getStr();
                        Assert.Equal($"NullableRange\r\n{1..^1}\r\n\r\n{^3..}", res);
                    }
                );
            }
        }

        [Fact]
        public async Task DontEmitDefaultNonTrivialAsync()
        {
            var data =
                new[]
                {
                    new _DontEmitDefaultNonTrivial("a"),
                    new _DontEmitDefaultNonTrivial(null),
                    new _DontEmitDefaultNonTrivial("ab"),
                    new _DontEmitDefaultNonTrivial("abc"),
                };

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(new _DontEmitDefaultNonTrivial_TypeDescriber()).ToOptions();

            await RunAsyncWriterVariants<_DontEmitDefaultNonTrivial>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteAllAsync(data);
                    }

                    var res = await getStr();
                    Assert.Equal("Bar\r\n1\r\n\r\n2\r\n3", res);
                }
            );
        }

        [Fact]
        public async Task VariousShouldSerializesAsync()
        {
            var t = typeof(_VariousShouldSerializes).GetTypeInfo();

            var foo = Getter.ForProperty(t.GetProperty(nameof(_VariousShouldSerializes.Foo)));
            var bar = Getter.ForProperty(t.GetProperty(nameof(_VariousShouldSerializes.Bar)));
            var fizz = Getter.ForProperty(t.GetProperty(nameof(_VariousShouldSerializes.Fizz)));

            var fooShould = Cesil.ShouldSerialize.ForMethod(t.GetMethod(nameof(_VariousShouldSerializes.Row_Context)));
            var barShould = Cesil.ShouldSerialize.ForMethod(t.GetMethod(nameof(_VariousShouldSerializes.NoRow_Context)));
            var fizzShould = Cesil.ShouldSerialize.ForMethod(t.GetMethod(nameof(_VariousShouldSerializes.Context)));

            var m = ManualTypeDescriber.CreateBuilder();
            m.WithExplicitGetter(t, "a", foo, Formatter.GetDefault(typeof(int).GetTypeInfo()), fooShould);
            m.WithExplicitGetter(t, "b", bar, Formatter.GetDefault(typeof(string).GetTypeInfo()), barShould);
            m.WithExplicitGetter(t, "c", fizz, Formatter.GetDefault(typeof(bool).GetTypeInfo()), fizzShould);

            var td = m.ToManualTypeDescriber();

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(td).ToOptions();

            await RunAsyncWriterVariants<_VariousShouldSerializes>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteAsync(new _VariousShouldSerializes { Foo = 123, Bar = "hello", Fizz = false });
                        await csv.WriteAsync(new _VariousShouldSerializes { Foo = 456, Bar = "world", Fizz = true });
                    }

                    var str = await getStr();
                    Assert.Equal("a,b,c\r\n123,hello,False\r\n456,world,True", str);
                }
            );
        }

        [Fact]
        public async Task TuplesAsync()
        {
            // Tuple
            await RunAsyncWriterVariants<Tuple<int, string, Guid>>(
                Options.Default,
                async (config, getWriter, getStr) =>
                {
                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteAsync(Tuple.Create(123, "hello", Guid.Parse("6ECB2B6D-D392-4222-B7F1-07E66A9A4259")));
                        await csv.WriteAsync(Tuple.Create(456, "world", Guid.Parse("7ECB2B6D-D492-4223-B7F2-7E66A9B42590")));
                    }

                    var str = await getStr();
                    Assert.Equal("Item1,Item2,Item3\r\n123,hello,6ecb2b6d-d392-4222-b7f1-07e66a9a4259\r\n456,world,7ecb2b6d-d492-4223-b7f2-7e66a9b42590", str);
                }
            );

            // Big Tuple
            Assert.Throws<InvalidOperationException>(() => Configuration.For<Tuple<int, int, int, int, int, int, int, Tuple<int, int>>>());

            // ValueTuple
            await RunAsyncWriterVariants<ValueTuple<int, string, Guid>>(
                Options.Default,
                async (config, getWriter, getStr) =>
                {
                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteAsync(ValueTuple.Create(123, "hello", Guid.Parse("6ECB2B6D-D392-4222-B7F1-07E66A9A4259")));
                        await csv.WriteAsync(ValueTuple.Create(456, "world", Guid.Parse("7ECB2B6D-D492-4223-B7F2-7E66A9B42590")));
                    }

                    var str = await getStr();
                    Assert.Equal("Item1,Item2,Item3\r\n123,hello,6ecb2b6d-d392-4222-b7f1-07e66a9a4259\r\n456,world,7ecb2b6d-d492-4223-b7f2-7e66a9b42590", str);
                }
            );

            // Big ValueTuple
            Assert.Throws<InvalidOperationException>(() => Configuration.For<ValueTuple<int, int, int, int, int, int, int, ValueTuple<int, int>>>());
        }

        [Fact]
        public async Task AnonymousTypeAsync()
        {
            await ThunkAsync(
                new { Foo = "hello world", Bar = 123 },
                "Foo,Bar\r\nhello world,123"
            );

            // to provide a way to get an anonymous generic type for the helpers
            static async Task ThunkAsync<T>(T ex, string expect)
            {
                // Tuple
                await RunAsyncWriterVariants<T>(
                    global::Cesil.Options.Default,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(ex);
                        }

                        var str = await getStr();
                        Assert.Equal(expect, str);
                    }
                );
            }
        }

        [Fact]
        public async Task ChainedFormattersAsync()
        {
            var ip = InstanceProvider.ForDelegate<_ChainedFormatters>((in ReadContext _, out _ChainedFormatters x) => { x = new _ChainedFormatters(); return true; });
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

            var m =
                ManualTypeDescriberBuilder
                    .CreateBuilder()
                    .WithInstanceProvider(ip)
                    .WithSerializableProperty(typeof(_ChainedFormatters).GetProperty(nameof(_ChainedFormatters.Foo)), "Foo", f)
                    .ToManualTypeDescriber();

            var opts = OptionsBuilder.CreateBuilder(Options.Default).WithTypeDescriber(m).ToOptions();

            await RunAsyncWriterVariants<_ChainedFormatters>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    var ctx = new _ChainedFormatters_Context();

                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer, ctx))
                    {
                        ctx.F = 1;
                        await csv.WriteAsync(new _ChainedFormatters { });
                        ctx.F = 2;
                        await csv.WriteAsync(new _ChainedFormatters { });
                        ctx.F = 3;
                        await csv.WriteAsync(new _ChainedFormatters { });
                        ctx.F = 1;
                        await csv.WriteAsync(new _ChainedFormatters { });
                    }

                    var str = await getStr();
                    Assert.Equal("Foo\r\n1234\r\nabc\r\n00\r\n1234", str);
                }
            );
        }

        [Fact]
        public async Task NoEscapesAsync()
        {
            // no escapes at all (TSV)
            {
                var opts = OptionsBuilder.CreateBuilder(Options.Default).WithValueSeparator("\t").WithEscapedValueStartAndEnd(null).WithEscapedValueEscapeCharacter(null).ToOptions();

                // correct
                await RunAsyncWriterVariants<_NoEscapes>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(new _NoEscapes { Foo = "abc", Bar = "123" });
                            await csv.WriteAsync(new _NoEscapes { Foo = "\"", Bar = "," });
                        }

                        var str = await getStr();
                        Assert.Equal("Foo\tBar\r\nabc\t123\r\n\"\t,", str);
                    }
                );

                // explodes if there's an value separator in a value, since there are no escapes
                {
                    // \t
                    await RunAsyncWriterVariants<_NoEscapes>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                var inv = await Assert.ThrowsAsync<InvalidOperationException>(async () => await csv.WriteAsync(new _NoEscapes { Foo = "\t", Bar = "foo" }));

                                Assert.Equal("Tried to write a value contain '\t' which requires escaping a value, but no way to escape a value is configured", inv.Message);
                            }

                            await getStr();
                        }
                    );

                    // \r\n
                    await RunAsyncWriterVariants<_NoEscapes>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                var inv = await Assert.ThrowsAsync<InvalidOperationException>(async () => await csv.WriteAsync(new _NoEscapes { Foo = "foo", Bar = "\r\n" }));

                                Assert.Equal("Tried to write a value contain '\r' which requires escaping a value, but no way to escape a value is configured", inv.Message);
                            }

                            await getStr();
                        }
                    );

                    var optsWithComment = OptionsBuilder.CreateBuilder(opts).WithCommentCharacter('#').ToOptions();
                    // #
                    await RunAsyncWriterVariants<_NoEscapes>(
                        optsWithComment,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                var inv = await Assert.ThrowsAsync<InvalidOperationException>(async () => await csv.WriteAsync(new _NoEscapes { Foo = "#", Bar = "fizz" }));

                                Assert.Equal("Tried to write a value contain '#' which requires escaping a value, but no way to escape a value is configured", inv.Message);
                            }

                            await getStr();
                        }
                    );
                }
            }

            // escapes, but no escape for the escape start and end char
            {
                var opts = OptionsBuilder.CreateBuilder(Options.Default).WithValueSeparator("\t").WithEscapedValueStartAndEnd('"').WithEscapedValueEscapeCharacter(null).ToOptions();

                // correct
                await RunAsyncWriterVariants<_NoEscapes>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(new _NoEscapes { Foo = "a\tbc", Bar = "#123" });
                            await csv.WriteAsync(new _NoEscapes { Foo = "\r", Bar = "," });
                        }

                        var str = await getStr();
                        Assert.Equal("Foo\tBar\r\n\"a\tbc\"\t#123\r\n\"\r\"\t,", str);
                    }
                );

                var optsWithComments = OptionsBuilder.CreateBuilder(opts).WithCommentCharacter('#').ToOptions();

                // correct with comments
                await RunAsyncWriterVariants<_NoEscapes>(
                    optsWithComments,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            await csv.WriteAsync(new _NoEscapes { Foo = "a\tbc", Bar = "#123" });
                            await csv.WriteAsync(new _NoEscapes { Foo = "\r", Bar = "," });
                        }

                        var str = await getStr();
                        Assert.Equal("Foo\tBar\r\n\"a\tbc\"\t\"#123\"\r\n\"\r\"\t,", str);
                    }
                );

                // explodes if there's an escape start character in a value, since it can't be escaped
                await RunAsyncWriterVariants<_NoEscapes>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer))
                        {
                            var inv = await Assert.ThrowsAsync<InvalidOperationException>(async () => await csv.WriteAsync(new _NoEscapes { Foo = "a\tbc", Bar = "\"" }));

                            Assert.Equal("Tried to write a value contain '\"' which requires escaping the character in an escaped value, but no way to escape inside an escaped value is configured", inv.Message);
                        }

                        await getStr();
                    }
                );
            }
        }

        [Fact]
        public async Task ErrorsAsync()
        {
            await RunAsyncWriterVariants<_Errors>(
                Options.Default,
                async (config, makeWriter, getStr) =>
                {
                    await using (var w = makeWriter())
                    await using (var csv = config.CreateAsyncWriter(w))
                    {
                        await Assert.ThrowsAsync<ArgumentNullException>(async () => await csv.WriteAllAsync(default(IEnumerable<_Errors>)));
                        await Assert.ThrowsAsync<ArgumentNullException>(async () => await csv.WriteAllAsync(default(IAsyncEnumerable<_Errors>)));

                        var exc = await Assert.ThrowsAsync<InvalidOperationException>(async () => await csv.WriteCommentAsync("foo"));
                        Assert.Equal($"No {nameof(Options.CommentCharacter)} configured, cannot write a comment line", exc.Message);
                    }

                    await getStr();
                }
            );
        }

        private sealed class _PipeWriterAsync
        {
            public string Fizz { get; set; }
            public int Buzz { get; set; }
        }

        [Fact]
        public async Task PipeWriterAsync()
        {
            var pipe = new Pipe();

            var config = Configuration.For<_PipeWriterAsync>();
            await using (var csv = config.CreateAsyncWriter(pipe.Writer, Encoding.UTF7))
            {
                await csv.WriteAsync(new _PipeWriterAsync { Fizz = "hello", Buzz = 12345 });
            }

            pipe.Writer.Complete();

            var bytes = new List<byte>();
            while (true)
            {
                var res = await pipe.Reader.ReadAsync();
                foreach (var seg in res.Buffer)
                {
                    bytes.AddRange(seg.ToArray());
                }

                if (res.IsCompleted || res.IsCanceled)
                {
                    break;
                }
            }

            var str = Encoding.UTF7.GetString(bytes.ToArray());

            Assert.Equal("Fizz,Buzz\r\nhello,12345", str);
        }

        [Fact]
        public async Task FailingGetterAsync()
        {
            var m = ManualTypeDescriberBuilder.CreateBuilder(ManualTypeDescriberFallbackBehavior.UseFallback);
            var t = typeof(_FailingGetter).GetTypeInfo();
            var g = Getter.ForMethod(t.GetProperty(nameof(_FailingGetter.Foo)).GetMethod);
            var f = Formatter.ForDelegate((int value, in WriteContext context, IBufferWriter<char> buffer) => false);

            m.WithInstanceProvider(InstanceProvider.ForDelegate((in ReadContext _, out _FailingGetter val) => { val = new _FailingGetter(); return true; }));
            m.WithExplicitGetter(t, "bar", g, f);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(m.ToManualTypeDescriber()).ToOptions();

            await RunAsyncWriterVariants<_FailingGetter>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    await using (var w = getWriter())
                    await using (var csv = config.CreateAsyncWriter(w))
                    {
                        await Assert.ThrowsAsync<SerializationException>(async () => await csv.WriteAsync(new _FailingGetter()));
                    }

                    var res = await getStr();
                    Assert.Equal("bar\r\n", res);
                }
            );
        }

        [Fact]
        public async Task LotsOfCommentsAsync()
        {
            var opts = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Always).ToOptions();

            await RunAsyncWriterVariants<_LotsOfComments>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    var cs = string.Join("\r\n", Enumerable.Repeat("foo", 1_000));

                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteCommentAsync(cs);
                    }

                    var str = await getStr();
                    var expected = nameof(_LotsOfComments.Hello) + "\r\n" + string.Join("\r\n", Enumerable.Repeat("#foo", 1_000));
                    Assert.Equal(expected, str);
                }
            );
        }

        [Fact]
        public async Task NullCommentErrorAsync()
        {
            await RunAsyncWriterVariants<_NullCommentError>(
                Options.Default,
                async (config, getWriter, getStr) =>
                {
                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await Assert.ThrowsAsync<ArgumentNullException>(async () => await csv.WriteCommentAsync(default(string)));
                    }

                    var _ = await getStr();
                    Assert.NotNull(_);
                }
            );
        }

        [Fact]
        public async Task WriteCommentAsync()
        {
            // no trailing new line
            {
                // first line, no headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    await RunAsyncWriterVariants<_WriteComment>(
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
                    await RunAsyncWriterVariants<_WriteComment>(
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
                    await RunAsyncWriterVariants<_WriteComment>(
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

                // first line, headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Always).ToOptions();

                    // empty line
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#", res);
                        }
                    );

                    // one line
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#hello", res);
                        }
                    );

                    // two lines
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello\r\nworld");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n#world", res);
                        }
                    );
                }

                // second line, no headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    await RunAsyncWriterVariants<_WriteComment>(
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
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello");
                            }

                            var res = await getStr();
                            Assert.Equal("123,456\r\n#hello", res);
                        }
                    );

                    // two lines
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello\r\nworld");
                            }

                            var res = await getStr();
                            Assert.Equal("123,456\r\n#hello\r\n#world", res);
                        }
                    );
                }

                // second line, headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Always).ToOptions();

                    // empty line
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#", res);
                        }
                    );

                    // one line
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello", res);
                        }
                    );

                    // two lines
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello\r\nworld");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello\r\n#world", res);
                        }
                    );
                }

                // before row, no headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    await RunAsyncWriterVariants<_WriteComment>(
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
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello");
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = await getStr();
                            Assert.Equal("#hello\r\n123,456", res);
                        }
                    );

                    // two lines
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello\r\nworld");
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = await getStr();
                            Assert.Equal("#hello\r\n#world\r\n123,456", res);
                        }
                    );
                }

                // before row, headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Always).ToOptions();

                    // empty line
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#", res);
                        }
                    );

                    // one line
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello");
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n123,456", res);
                        }
                    );

                    // two lines
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello\r\nworld");
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n#world\r\n123,456", res);
                        }
                    );
                }
            }

            // trailing new line
            {
                // first line, no headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    await RunAsyncWriterVariants<_WriteComment>(
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
                    await RunAsyncWriterVariants<_WriteComment>(
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
                    await RunAsyncWriterVariants<_WriteComment>(
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

                // first line, headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Always).ToOptions();

                    // empty line
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#\r\n", res);
                        }
                    );

                    // one line
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n", res);
                        }
                    );

                    // two lines
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello\r\nworld");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n#world\r\n", res);
                        }
                    );
                }

                // second line, no headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    await RunAsyncWriterVariants<_WriteComment>(
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
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello");
                            }

                            var res = await getStr();
                            Assert.Equal("123,456\r\n#hello\r\n", res);
                        }
                    );

                    // two lines
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello\r\nworld");
                            }

                            var res = await getStr();
                            Assert.Equal("123,456\r\n#hello\r\n#world\r\n", res);
                        }
                    );
                }

                // second line, headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Always).ToOptions();

                    // empty line
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#\r\n", res);
                        }
                    );

                    // one line
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello\r\n", res);
                        }
                    );

                    // two lines
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                                await csv.WriteCommentAsync("hello\r\nworld");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n123,456\r\n#hello\r\n#world\r\n", res);
                        }
                    );
                }

                // before row, no headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Never).ToOptions();

                    // empty line
                    await RunAsyncWriterVariants<_WriteComment>(
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
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello");
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = await getStr();
                            Assert.Equal("#hello\r\n123,456\r\n", res);
                        }
                    );

                    // two lines
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello\r\nworld");
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = await getStr();
                            Assert.Equal("#hello\r\n#world\r\n123,456\r\n", res);
                        }
                    );
                }

                // before row, headers
                {
                    var opts = Options.CreateBuilder(Options.Default).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).WithCommentCharacter('#').WithWriteHeader(WriteHeader.Always).ToOptions();

                    // empty line
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("");
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#\r\n", res);
                        }
                    );

                    // one line
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello");
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n123,456\r\n", res);
                        }
                    );

                    // two lines
                    await RunAsyncWriterVariants<_WriteComment>(
                        opts,
                        async (config, getWriter, getStr) =>
                        {
                            await using (var writer = getWriter())
                            await using (var csv = config.CreateAsyncWriter(writer))
                            {
                                await csv.WriteCommentAsync("hello\r\nworld");
                                await csv.WriteAsync(new _WriteComment { Foo = 123, Bar = 456 });
                            }

                            var res = await getStr();
                            Assert.Equal("Foo,Bar\r\n#hello\r\n#world\r\n123,456\r\n", res);
                        }
                    );
                }
            }
        }

        [Fact]
        public async Task DelegateStaticShouldSerializeAsync()
        {
            var shouldSerializeCalled = 0;
            StaticShouldSerializeDelegate shouldSerializeDel =
                (in WriteContext _) =>
                {
                    shouldSerializeCalled++;

                    return false;
                };

            var name = nameof(_DelegateShouldSerialize.Foo);
            var getter = (Getter)typeof(_DelegateShouldSerialize).GetProperty(nameof(_DelegateShouldSerialize.Foo)).GetMethod;
            var formatter = Formatter.GetDefault(typeof(int).GetTypeInfo());
            var shouldSerialize = Cesil.ShouldSerialize.ForDelegate(shouldSerializeDel);

            var describer = ManualTypeDescriberBuilder.CreateBuilder();
            describer.WithExplicitGetter(typeof(_DelegateShouldSerialize).GetTypeInfo(), name, getter, formatter, shouldSerialize);
            InstanceProviderDelegate<_DelegateShouldSerialize> del = (in ReadContext _, out _DelegateShouldSerialize i) => { i = new _DelegateShouldSerialize(); return true; };
            describer.WithInstanceProvider((InstanceProvider)del);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)describer.ToManualTypeDescriber()).WithWriteHeader(WriteHeader.Always).ToOptions();

            await RunAsyncWriterVariants<_DelegateShouldSerialize>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    shouldSerializeCalled = 0;

                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteAsync(new _DelegateShouldSerialize { Foo = 123 });
                        await csv.WriteAsync(new _DelegateShouldSerialize { Foo = 0 });
                        await csv.WriteAsync(new _DelegateShouldSerialize { Foo = 456 });
                    }

                    var res = await getStr();
                    Assert.Equal("Foo\r\n\r\n\r\n", res);

                    Assert.Equal(3, shouldSerializeCalled);
                }
            );
        }

        [Fact]
        public async Task DelegateShouldSerializeAsync()
        {
            var shouldSerializeCalled = 0;
            ShouldSerializeDelegate<_DelegateShouldSerialize> shouldSerializeDel =
                (_DelegateShouldSerialize row, in WriteContext _) =>
                {
                    shouldSerializeCalled++;

                    return row.Foo % 2 != 0;
                };

            var name = nameof(_DelegateShouldSerialize.Foo);
            var getter = (Getter)typeof(_DelegateShouldSerialize).GetProperty(nameof(_DelegateShouldSerialize.Foo)).GetMethod;
            var formatter = Formatter.GetDefault(typeof(int).GetTypeInfo());
            var shouldSerialize = Cesil.ShouldSerialize.ForDelegate(shouldSerializeDel);

            var describer = ManualTypeDescriberBuilder.CreateBuilder();
            describer.WithExplicitGetter(typeof(_DelegateShouldSerialize).GetTypeInfo(), name, getter, formatter, shouldSerialize);
            InstanceProviderDelegate<_DelegateShouldSerialize> del = (in ReadContext _, out _DelegateShouldSerialize i) => { i = new _DelegateShouldSerialize(); return true; };
            describer.WithInstanceProvider((InstanceProvider)del);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)describer.ToManualTypeDescriber()).WithWriteHeader(WriteHeader.Always).ToOptions();

            await RunAsyncWriterVariants<_DelegateShouldSerialize>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    shouldSerializeCalled = 0;

                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteAsync(new _DelegateShouldSerialize { Foo = 123 });
                        await csv.WriteAsync(new _DelegateShouldSerialize { Foo = 0 });
                        await csv.WriteAsync(new _DelegateShouldSerialize { Foo = 456 });
                    }

                    var res = await getStr();
                    Assert.Equal("Foo\r\n123\r\n\r\n", res);

                    Assert.Equal(3, shouldSerializeCalled);
                }
            );
        }

        [Fact]
        public async Task DelegateFormatterAsync()
        {
            var formatterCalled = 0;
            FormatterDelegate<int> formatDel =
                (int val, in WriteContext _, IBufferWriter<char> buffer) =>
                {
                    formatterCalled++;

                    var s = val.ToString();

                    var span = s.AsSpan();
                    while (!span.IsEmpty)
                    {
                        var writeTo = buffer.GetSpan(span.Length);
                        var len = Math.Min(span.Length, writeTo.Length);

                        var toWrite = span.Slice(0, len);
                        toWrite.CopyTo(writeTo);
                        buffer.Advance(len);

                        span = span.Slice(len);
                    }

                    span = s.AsSpan();
                    while (!span.IsEmpty)
                    {
                        var writeTo = buffer.GetSpan(span.Length);
                        var len = Math.Min(span.Length, writeTo.Length);

                        var toWrite = span.Slice(0, len);
                        toWrite.CopyTo(writeTo);
                        buffer.Advance(len);

                        span = span.Slice(len);
                    }

                    return true;
                };

            var name = nameof(_DelegateFormatter.Foo);
            var getter = (Getter)typeof(_DelegateFormatter).GetProperty(nameof(_DelegateFormatter.Foo)).GetMethod;
            var formatter = Formatter.ForDelegate(formatDel);

            var describer = ManualTypeDescriberBuilder.CreateBuilder();
            describer.WithExplicitGetter(typeof(_DelegateFormatter).GetTypeInfo(), name, getter, formatter);
            InstanceProviderDelegate<_DelegateFormatter> del = (in ReadContext _, out _DelegateFormatter i) => { i = new _DelegateFormatter(); return true; };
            describer.WithInstanceProvider((InstanceProvider)del);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)describer.ToManualTypeDescriber()).WithWriteHeader(WriteHeader.Always).ToOptions();

            await RunAsyncWriterVariants<_DelegateFormatter>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    formatterCalled = 0;

                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteAsync(new _DelegateFormatter { Foo = 123 });
                        await csv.WriteAsync(new _DelegateFormatter { Foo = 0 });
                        await csv.WriteAsync(new _DelegateFormatter { Foo = 456 });
                    }

                    var res = await getStr();
                    Assert.Equal("Foo\r\n123123\r\n00\r\n456456", res);

                    Assert.Equal(3, formatterCalled);
                }
            );
        }

        [Fact]
        public async Task DelegateStaticGetterAsync()
        {
            var getterCalled = 0;
            StaticGetterDelegate<int> getDel =
                (in WriteContext _) =>
                {
                    getterCalled++;

                    return getterCalled;
                };

            var name = nameof(_DelegateGetter.Foo);
            var getter = Getter.ForDelegate(getDel);

            var describer = ManualTypeDescriberBuilder.CreateBuilder();
            describer.WithExplicitGetter(typeof(_DelegateGetter).GetTypeInfo(), name, getter);
            InstanceProviderDelegate<_DelegateGetter> del = (in ReadContext _, out _DelegateGetter i) => { i = new _DelegateGetter(); return true; };
            describer.WithInstanceProvider((InstanceProvider)del);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)describer.ToManualTypeDescriber()).WithWriteHeader(WriteHeader.Always).ToOptions();

            await RunAsyncWriterVariants<_DelegateGetter>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    getterCalled = 0;

                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteAsync(new _DelegateGetter { Foo = 123 });
                        await csv.WriteAsync(new _DelegateGetter { Foo = 0 });
                        await csv.WriteAsync(new _DelegateGetter { Foo = 456 });
                    }

                    var res = await getStr();
                    Assert.Equal("Foo\r\n1\r\n2\r\n3", res);

                    Assert.Equal(3, getterCalled);
                }
            );
        }

        [Fact]
        public async Task DelegateGetterAsync()
        {
            var getterCalled = 0;
            GetterDelegate<_DelegateGetter, int> getDel =
                (_DelegateGetter row, in WriteContext _) =>
                {
                    getterCalled++;

                    return row.Foo * 2;
                };

            var name = nameof(_DelegateGetter.Foo);
            var getter = Getter.ForDelegate(getDel);

            var describer = ManualTypeDescriberBuilder.CreateBuilder();
            describer.WithExplicitGetter(typeof(_DelegateGetter).GetTypeInfo(), name, getter);
            InstanceProviderDelegate<_DelegateGetter> del = (in ReadContext _, out _DelegateGetter i) => { i = new _DelegateGetter(); return true; };
            describer.WithInstanceProvider((InstanceProvider)del);

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)describer.ToManualTypeDescriber()).WithWriteHeader(WriteHeader.Always).ToOptions();

            await RunAsyncWriterVariants<_DelegateGetter>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    getterCalled = 0;

                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteAsync(new _DelegateGetter { Foo = 123 });
                        await csv.WriteAsync(new _DelegateGetter { Foo = 0 });
                        await csv.WriteAsync(new _DelegateGetter { Foo = 456 });
                    }

                    var res = await getStr();
                    Assert.Equal("Foo\r\n246\r\n0\r\n912", res);

                    Assert.Equal(3, getterCalled);
                }
            );
        }

        [Fact]
        public async Task UserDefinedEmitDefaultValueAsync()
        {
            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(new _UserDefinedEmitDefaultValue_TypeDescripter()).ToOptions();

            // not equatable
            await RunAsyncWriterVariants<_UserDefinedEmitDefaultValue1>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteAsync(new _UserDefinedEmitDefaultValue1 { Foo = "hello", Bar = default });
                        await csv.WriteAsync(new _UserDefinedEmitDefaultValue1 { Foo = "world", Bar = new _UserDefinedEmitDefaultValue_ValueType { Value = 2 } });
                    }

                    var res = await getStr();
                    Assert.Equal("Foo,Bar\r\nhello,\r\nworld,2", res);
                }
            );

            // equatable
            await RunAsyncWriterVariants<_UserDefinedEmitDefaultValue2>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    _UserDefinedEmitDefaultValue_ValueType_Equatable.EqualsCallCount = 0;

                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteAsync(new _UserDefinedEmitDefaultValue2 { Foo = "hello", Bar = default });
                        await csv.WriteAsync(new _UserDefinedEmitDefaultValue2 { Foo = "world", Bar = new _UserDefinedEmitDefaultValue_ValueType_Equatable { Value = 2 } });
                    }

                    var res = await getStr();
                    Assert.Equal("Foo,Bar\r\nhello,\r\nworld,2", res);
                    Assert.Equal(2, _UserDefinedEmitDefaultValue_ValueType_Equatable.EqualsCallCount);
                }
            );

            // operator
            await RunAsyncWriterVariants<_UserDefinedEmitDefaultValue3>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    _UserDefinedEmitDefaultValue_ValueType_Operator.OperatorCallCount = 0;

                    await using (var writer = getWriter())
                    await using (var csv = config.CreateAsyncWriter(writer))
                    {
                        await csv.WriteAsync(new _UserDefinedEmitDefaultValue3 { Foo = "hello", Bar = default });
                        await csv.WriteAsync(new _UserDefinedEmitDefaultValue3 { Foo = "world", Bar = new _UserDefinedEmitDefaultValue_ValueType_Operator { Value = 2 } });
                    }

                    var res = await getStr();
                    Assert.Equal("Foo,Bar\r\nhello,\r\nworld,2", res);
                    Assert.Equal(2, _UserDefinedEmitDefaultValue_ValueType_Operator.OperatorCallCount);
                }
            );
        }

        [Fact]
        public async Task ContextAsync()
        {
            var formatFoo = (Formatter)typeof(WriterTests).GetMethod(nameof(_Context_FormatFoo));
            var formatBar = (Formatter)typeof(WriterTests).GetMethod(nameof(_Context_FormatBar));

            var describer = ManualTypeDescriberBuilder.CreateBuilder(ManualTypeDescriberFallbackBehavior.UseFallback);
            describer.WithInstanceProvider((InstanceProvider)typeof(_Context).GetConstructor(Type.EmptyTypes));
            describer.WithSerializableProperty(typeof(_Context).GetProperty(nameof(_Context.Foo)), nameof(_Context.Foo), formatFoo);
            describer.WithSerializableProperty(typeof(_Context).GetProperty(nameof(_Context.Bar)), nameof(_Context.Bar), formatBar);

            var optsBase = Options.CreateBuilder(Options.Default).WithTypeDescriber(describer.ToManualTypeDescriber());

            // no headers
            {
                var opts = optsBase.WithWriteHeader(WriteHeader.Never).ToOptions();

                await RunAsyncWriterVariants<_Context>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        _Context_FormatFoo_Records = new List<string>();
                        _Context_FormatBar_Records = new List<string>();

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer, "context!"))
                        {
                            await csv.WriteAsync(new _Context { Bar = 123, Foo = "whatever" });
                            await csv.WriteAsync(new _Context { Bar = 456, Foo = "indeed" });
                        }

                        var res = await getStr();
                        Assert.Equal("whatever,123\r\nindeed,456", res);

                        Assert.Collection(
                            _Context_FormatFoo_Records,
                            c => Assert.Equal("0,Foo,0,whatever,context!", c),
                            c => Assert.Equal("1,Foo,0,indeed,context!", c)
                        );

                        Assert.Collection(
                            _Context_FormatBar_Records,
                            c => Assert.Equal("0,Bar,1,123,context!", c),
                            c => Assert.Equal("1,Bar,1,456,context!", c)
                        );
                    }
                );
            }

            // with headers
            {
                var opts = optsBase.WithWriteHeader(WriteHeader.Always).ToOptions();

                await RunAsyncWriterVariants<_Context>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        _Context_FormatFoo_Records = new List<string>();
                        _Context_FormatBar_Records = new List<string>();

                        await using (var writer = getWriter())
                        await using (var csv = config.CreateAsyncWriter(writer, "context!"))
                        {
                            await csv.WriteAsync(new _Context { Bar = 123, Foo = "whatever" });
                            await csv.WriteAsync(new _Context { Bar = 456, Foo = "indeed" });
                        }

                        var res = await getStr();
                        Assert.Equal("Foo,Bar\r\nwhatever,123\r\nindeed,456", res);

                        Assert.Collection(
                            _Context_FormatFoo_Records,
                            c => Assert.Equal("0,Foo,0,whatever,context!", c),
                            c => Assert.Equal("1,Foo,0,indeed,context!", c)
                        );

                        Assert.Collection(
                            _Context_FormatBar_Records,
                            c => Assert.Equal("0,Bar,1,123,context!", c),
                            c => Assert.Equal("1,Bar,1,456,context!", c)
                        );
                    }
                );
            }
        }

        [Fact]
        public async Task CommentEscapeAsync()
        {
            // \r\n
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Never).WithRowEnding(RowEnding.CarriageReturnLineFeed).WithCommentCharacter('#').WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).ToOptions();

                await RunAsyncWriterVariants<_CommentEscape>(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _CommentEscape { A = "#hello", B = "foo" });
                        }

                        var txt = await getString();
                        Assert.Equal("\"#hello\",foo\r\n", txt);
                    }
                );

                await RunAsyncWriterVariants<_CommentEscape>(
                    opts,
                     async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _CommentEscape { A = "hello", B = "fo#o" });
                        }

                        var txt = await getString();
                        Assert.Equal("hello,\"fo#o\"\r\n", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Never).WithRowEnding(RowEnding.CarriageReturn).WithCommentCharacter('#').WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).ToOptions();

                await RunAsyncWriterVariants<_CommentEscape>(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _CommentEscape { A = "#hello", B = "foo" });
                        }

                        var txt = await getString();
                        Assert.Equal("\"#hello\",foo\r", txt);
                    }
                );

                await RunAsyncWriterVariants<_CommentEscape>(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _CommentEscape { A = "hello", B = "fo#o" });
                        }

                        var txt = await getString();
                        Assert.Equal("hello,\"fo#o\"\r", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Never).WithRowEnding(RowEnding.LineFeed).WithCommentCharacter('#').WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).ToOptions();

                await RunAsyncWriterVariants<_CommentEscape>(
                    opts,
                     async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _CommentEscape { A = "#hello", B = "foo" });
                        }

                        var txt = await getString();
                        Assert.Equal("\"#hello\",foo\n", txt);
                    }
                );

                await RunAsyncWriterVariants<_CommentEscape>(
                    opts,
                    async (config, getWriter, getString) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _CommentEscape { A = "hello", B = "fo#o" });
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
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.CarriageReturnLineFeed).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).ToOptions();

                await RunAsyncWriterVariants<_EscapeHeaders>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _EscapeHeaders { A = "fizz", B = "buzz", C = "yes" });
                            await writer.WriteAsync(new _EscapeHeaders { A = "ping", B = "pong", C = "no" });
                        }

                        var txt = await getStr();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\r\nfizz,buzz,yes\r\nping,pong,no\r\n", txt);
                    }
                );

                // empty
                await RunAsyncWriterVariants<_EscapeHeaders>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                        }

                        var txt = await getStr();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\r\n", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.CarriageReturn).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).ToOptions();

                await RunAsyncWriterVariants<_EscapeHeaders>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _EscapeHeaders { A = "fizz", B = "buzz", C = "yes" });
                            await writer.WriteAsync(new _EscapeHeaders { A = "ping", B = "pong", C = "no" });
                        }

                        var txt = await getStr();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\rfizz,buzz,yes\rping,pong,no\r", txt);
                    }
                );

                // empty
                await RunAsyncWriterVariants<_EscapeHeaders>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                        }

                        var txt = await getStr();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\r", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.LineFeed).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).ToOptions();

                await RunAsyncWriterVariants<_EscapeHeaders>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _EscapeHeaders { A = "fizz", B = "buzz", C = "yes" });
                            await writer.WriteAsync(new _EscapeHeaders { A = "ping", B = "pong", C = "no" });
                        }

                        var txt = await getStr();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\nfizz,buzz,yes\nping,pong,no\n", txt);
                    }
                );

                // empty
                await RunAsyncWriterVariants<_EscapeHeaders>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                        }

                        var txt = await getStr();
                        Assert.Equal("\"hello\r\nworld\",\"foo,bar\",yup\n", txt);
                    }
                );
            }
        }

        [Fact]
        public async Task HeadersAsync()
        {
            // \r\n
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

                await RunAsyncWriterVariants<_Headers>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _Headers { Foo = "hello", Bar = 123 });
                            await writer.WriteAsync(new _Headers { Foo = "foo", Bar = 789 });
                        }

                        var txt = await getStr();
                        Assert.Equal("Foo,Bar\r\nhello,123\r\nfoo,789", txt);
                    }
                );

                // empty
                await RunAsyncWriterVariants<_Headers>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                        }

                        var txt = await getStr();
                        Assert.Equal("Foo,Bar", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.CarriageReturn).ToOptions();

                await RunAsyncWriterVariants<_Headers>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _Headers { Foo = "hello", Bar = 123 });
                            await writer.WriteAsync(new _Headers { Foo = "foo", Bar = 789 });
                        }

                        var txt = await getStr();
                        Assert.Equal("Foo,Bar\rhello,123\rfoo,789", txt);
                    }
                );

                // empty
                await RunAsyncWriterVariants<_Headers>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                        }

                        var txt = await getStr();
                        Assert.Equal("Foo,Bar", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.LineFeed).ToOptions();

                await RunAsyncWriterVariants<_Headers>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _Headers { Foo = "hello", Bar = 123 });
                            await writer.WriteAsync(new _Headers { Foo = "foo", Bar = 789 });
                        }

                        var txt = await getStr();
                        Assert.Equal("Foo,Bar\nhello,123\nfoo,789", txt);
                    }
                );

                // empty
                await RunAsyncWriterVariants<_Headers>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                        }

                        var txt = await getStr();
                        Assert.Equal("Foo,Bar", txt);
                    }
                );
            }
        }

        [Fact]
        public async Task MultiSegmentValueAsync()
        {
            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber(new _MultiSegmentValue_TypeDescriber()).ToOptions();

            // no encoding
            await RunAsyncWriterVariants<_MultiSegmentValue>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    await using (var writer = config.CreateAsyncWriter(getWriter()))
                    {
                        var row = new _MultiSegmentValue { Foo = string.Join("", Enumerable.Repeat('c', 5_000)) };
                        await writer.WriteAsync(row);
                    }

                    var txt = await getStr();
                    Assert.Equal("Foo\r\n" + string.Join("", Enumerable.Repeat('c', 5_000)), txt);
                }
            );

            // quoted
            await RunAsyncWriterVariants<_MultiSegmentValue>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    await using (var writer = config.CreateAsyncWriter(getWriter()))
                    {
                        var row = new _MultiSegmentValue { Foo = string.Join("", Enumerable.Repeat("d,", 5_000)) };
                        await writer.WriteAsync(row);
                    }

                    var txt = await getStr();
                    Assert.Equal("Foo\r\n\"" + string.Join("", Enumerable.Repeat("d,", 5_000)) + "\"", txt);
                }
            );

            // escaped
            await RunAsyncWriterVariants<_MultiSegmentValue>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    await using (var writer = config.CreateAsyncWriter(getWriter()))
                    {
                        var row = new _MultiSegmentValue { Foo = string.Join("", Enumerable.Repeat("foo\"bar", 1_000)) };
                        await writer.WriteAsync(row);
                    }

                    var txt = await getStr();
                    Assert.Equal("Foo\r\n\"" + string.Join("", Enumerable.Repeat("foo\"\"bar", 1_000)) + "\"", txt);
                }
            );
        }

        [Fact]
        public async Task NeedEscapeAsync()
        {
            // \r\n
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Never).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

                await RunAsyncWriterVariants<_Simple>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _Simple { Foo = "hello,world", Bar = 123, Nope = 456 });
                            await writer.WriteAsync(new _Simple { Foo = "foo\"bar", Bar = 789, Nope = null });
                            await writer.WriteAsync(new _Simple { Foo = "fizz\r\nbuzz", Bar = -12, Nope = 34 });
                        }

                        var txt = await getStr();
                        Assert.Equal("\"hello,world\",123,456\r\n\"foo\"\"bar\",789,\r\n\"fizz\r\nbuzz\",-12,34", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Never).WithRowEnding(RowEnding.CarriageReturn).ToOptions();

                await RunAsyncWriterVariants<_Simple>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _Simple { Foo = "hello,world", Bar = 123, Nope = 456 });
                            await writer.WriteAsync(new _Simple { Foo = "foo\"bar", Bar = 789, Nope = null });
                            await writer.WriteAsync(new _Simple { Foo = "fizz\r\nbuzz", Bar = -12, Nope = 34 });
                        }

                        var txt = await getStr();
                        Assert.Equal("\"hello,world\",123,456\r\"foo\"\"bar\",789,\r\"fizz\r\nbuzz\",-12,34", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Never).WithRowEnding(RowEnding.LineFeed).ToOptions();

                await RunAsyncWriterVariants<_Simple>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _Simple { Foo = "hello,world", Bar = 123, Nope = 456 });
                            await writer.WriteAsync(new _Simple { Foo = "foo\"bar", Bar = 789, Nope = null });
                            await writer.WriteAsync(new _Simple { Foo = "fizz\r\nbuzz", Bar = -12, Nope = 34 });
                        }

                        var txt = await getStr();
                        Assert.Equal("\"hello,world\",123,456\n\"foo\"\"bar\",789,\n\"fizz\r\nbuzz\",-12,34", txt);
                    }
                );
            }

            // large
            {
                var opts = Options.Default;
                var val = string.Join("", Enumerable.Repeat("abc\r\n", 450));

                await RunAsyncWriterVariants<_Simple>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _Simple { Foo = val, Bar = 001, Nope = 009 });
                        }

                        var txt = await getStr();
                        Assert.Equal("Foo,Bar,Nope\r\n\"abc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\nabc\r\n\",1,9", txt);
                    }
                );
            }
        }

        [Fact]
        public async Task ShouldSerializeAsync()
        {
            var opts = Options.Default;

            await RunAsyncWriterVariants<_ShouldSerialize>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    _ShouldSerialize.Reset();

                    await using (var csv = config.CreateAsyncWriter(getWriter()))
                    {
                        await csv.WriteAsync(new _ShouldSerialize { Foo = 1, Bar = "hello" });
                        _ShouldSerialize.OnOff = !_ShouldSerialize.OnOff;
                        await csv.WriteAsync(new _ShouldSerialize { Foo = 3, Bar = "world" });
                        _ShouldSerialize.OnOff = !_ShouldSerialize.OnOff;
                        await csv.WriteAsync(new _ShouldSerialize { Foo = 4, Bar = "fizz" });
                        _ShouldSerialize.OnOff = !_ShouldSerialize.OnOff;
                        await csv.WriteAsync(new _ShouldSerialize { Foo = 9, Bar = "buzz" });
                        _ShouldSerialize.OnOff = true;
                        await csv.WriteAsync(new _ShouldSerialize { Foo = 10, Bar = "bonzai" });
                    }

                    var txt = await getStr();
                    Assert.Equal("Foo,Bar\r\n,\r\n,world\r\n4,\r\n,buzz\r\n10,bonzai", txt);
                }
            );
        }

        [Fact]
        public async Task VariousGettersAsync()
        {
            var m = ManualTypeDescriberBuilder.CreateBuilder();
            m.WithInstanceProvider((InstanceProvider)typeof(_VariousGetters).GetConstructor(Type.EmptyTypes));
            m.WithExplicitGetter(typeof(_VariousGetters).GetTypeInfo(), "Bar", (Getter)typeof(_VariousGetters).GetMethod("GetBar", BindingFlags.Static | BindingFlags.Public));
            m.WithExplicitGetter(typeof(_VariousGetters).GetTypeInfo(), "Fizz", (Getter)typeof(_VariousGetters).GetMethod("GetFizz", BindingFlags.Static | BindingFlags.Public));
            m.WithExplicitGetter(typeof(_VariousGetters).GetTypeInfo(), "Buzz", (Getter)typeof(_VariousGetters).GetMethod("GetBuzz", BindingFlags.Static | BindingFlags.Public));
            m.WithExplicitGetter(typeof(_VariousGetters).GetTypeInfo(), "Hello", (Getter)typeof(_VariousGetters).GetMethod("GetHello", BindingFlags.Static | BindingFlags.Public));
            m.WithExplicitGetter(typeof(_VariousGetters).GetTypeInfo(), "World", (Getter)typeof(_VariousGetters).GetMethod("GetWorld", BindingFlags.Instance | BindingFlags.Public));

            var opts = Options.CreateBuilder(Options.Default).WithTypeDescriber((ITypeDescriber)m.ToManualTypeDescriber()).ToOptions();

            await RunAsyncWriterVariants<_VariousGetters>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    await using (var csv = config.CreateAsyncWriter(getWriter()))
                    {
                        await csv.WriteAsync(new _VariousGetters(1));
                        await csv.WriteAsync(new _VariousGetters(2));
                        await csv.WriteAsync(new _VariousGetters(3));
                    }

                    var str = await getStr();
                    Assert.Equal("Bar,Fizz,Buzz,Hello,World\r\n2,3,3,5,5\r\n2,4,3,6,5\r\n2,5,3,7,5", str);
                }
            );
        }

        [Fact]
        public async Task SimpleAsync()
        {
            // \r\n
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Never).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

                await RunAsyncWriterVariants<_Simple>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _Simple { Foo = "hello", Bar = 123, Nope = 456 });
                            await writer.WriteAsync(new _Simple { Foo = null, Bar = 789, Nope = null });
                        }

                        var txt = await getStr();
                        Assert.Equal("hello,123,456\r\n,789,", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Never).WithRowEnding(RowEnding.LineFeed).ToOptions();

                await RunAsyncWriterVariants<_Simple>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _Simple { Foo = "hello", Bar = 123, Nope = 456 });
                            await writer.WriteAsync(new _Simple { Foo = null, Bar = 789, Nope = null });
                        }

                        var txt = await getStr();
                        Assert.Equal("hello,123,456\n,789,", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Never).WithRowEnding(RowEnding.CarriageReturn).ToOptions();

                await RunAsyncWriterVariants<_Simple>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _Simple { Foo = "hello", Bar = 123, Nope = 456 });
                            await writer.WriteAsync(new _Simple { Foo = null, Bar = 789, Nope = null });
                        }

                        var txt = await getStr();
                        Assert.Equal("hello,123,456\r,789,", txt);
                    }
                );
            }
        }

        [Fact]
        public async Task WriteAllAsync()
        {
            // \r\n
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

                await RunAsyncWriterVariants<_WriteAll>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAllAsync(
                                new[]
                                {
                                new _WriteAll { Bar = 123, Buzz = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = "hello" },
                                new _WriteAll { Bar = 456, Buzz = new DateTimeOffset(1980, 2, 2, 1, 1, 1, TimeSpan.Zero), Fizz = null, Foo = "hello" },
                                new _WriteAll { Bar = 789, Buzz = new DateTimeOffset(1990, 3, 3, 2, 2, 2, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = null },
                                new _WriteAll { Bar = 012, Buzz = new DateTimeOffset(2000, 4, 4, 3, 3, 3, TimeSpan.Zero), Fizz = null, Foo = null }
                                }
                            );
                        }

                        var txt = await getStr();
                        Assert.Equal("Foo,Bar,Fizz,Buzz\r\nhello,123,5dc798f5-6477-4216-8567-9d17c05fa87e,1970-01-01 00:00:00Z\r\nhello,456,,1980-02-02 01:01:01Z\r\n,789,5dc798f5-6477-4216-8567-9d17c05fa87e,1990-03-03 02:02:02Z\r\n,12,,2000-04-04 03:03:03Z", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.CarriageReturn).ToOptions();

                await RunAsyncWriterVariants<_WriteAll>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAllAsync(
                                new[]
                                {
                                    new _WriteAll { Bar = 123, Buzz = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = "hello" },
                                    new _WriteAll { Bar = 456, Buzz = new DateTimeOffset(1980, 2, 2, 1, 1, 1, TimeSpan.Zero), Fizz = null, Foo = "hello" },
                                    new _WriteAll { Bar = 789, Buzz = new DateTimeOffset(1990, 3, 3, 2, 2, 2, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = null },
                                    new _WriteAll { Bar = 012, Buzz = new DateTimeOffset(2000, 4, 4, 3, 3, 3, TimeSpan.Zero), Fizz = null, Foo = null }
                                }
                            );
                        }

                        var txt = await getStr();
                        Assert.Equal("Foo,Bar,Fizz,Buzz\rhello,123,5dc798f5-6477-4216-8567-9d17c05fa87e,1970-01-01 00:00:00Z\rhello,456,,1980-02-02 01:01:01Z\r,789,5dc798f5-6477-4216-8567-9d17c05fa87e,1990-03-03 02:02:02Z\r,12,,2000-04-04 03:03:03Z", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.LineFeed).ToOptions();

                await RunAsyncWriterVariants<_WriteAll>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAllAsync(
                                new[]
                                {
                                    new _WriteAll { Bar = 123, Buzz = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = "hello" },
                                    new _WriteAll { Bar = 456, Buzz = new DateTimeOffset(1980, 2, 2, 1, 1, 1, TimeSpan.Zero), Fizz = null, Foo = "hello" },
                                    new _WriteAll { Bar = 789, Buzz = new DateTimeOffset(1990, 3, 3, 2, 2, 2, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = null },
                                    new _WriteAll { Bar = 012, Buzz = new DateTimeOffset(2000, 4, 4, 3, 3, 3, TimeSpan.Zero), Fizz = null, Foo = null }
                                }
                            );
                        }

                        var txt = await getStr();
                        Assert.Equal("Foo,Bar,Fizz,Buzz\nhello,123,5dc798f5-6477-4216-8567-9d17c05fa87e,1970-01-01 00:00:00Z\nhello,456,,1980-02-02 01:01:01Z\n,789,5dc798f5-6477-4216-8567-9d17c05fa87e,1990-03-03 02:02:02Z\n,12,,2000-04-04 03:03:03Z", txt);
                    }
                );
            }
        }

        [Fact]
        public async Task WriteAllAsync_Enumerable()
        {
            var rows =
                new[]
                {
                    new _WriteAll { Bar = 123, Buzz = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = "hello" },
                    new _WriteAll { Bar = 456, Buzz = new DateTimeOffset(1980, 2, 2, 1, 1, 1, TimeSpan.Zero), Fizz = null, Foo = "hello" },
                    new _WriteAll { Bar = 789, Buzz = new DateTimeOffset(1990, 3, 3, 2, 2, 2, TimeSpan.Zero), Fizz = new Guid("5DC798F5-6477-4216-8567-9D17C05FA87E"), Foo = null },
                    new _WriteAll { Bar = 012, Buzz = new DateTimeOffset(2000, 4, 4, 3, 3, 3, TimeSpan.Zero), Fizz = null, Foo = null }
                };

            // enumerable is sync
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();

                await RunAsyncWriterVariants<_WriteAll>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAllAsync(rows);
                        }

                        var txt = await getStr();
                        Assert.Equal("Foo,Bar,Fizz,Buzz\r\nhello,123,5dc798f5-6477-4216-8567-9d17c05fa87e,1970-01-01 00:00:00Z\r\nhello,456,,1980-02-02 01:01:01Z\r\n,789,5dc798f5-6477-4216-8567-9d17c05fa87e,1990-03-03 02:02:02Z\r\n,12,,2000-04-04 03:03:03Z", txt);
                    }
                );
            }

            // enumerable is async
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.CarriageReturnLineFeed).ToOptions();
                var enumerable = new TestAsyncEnumerable<_WriteAll>(rows, true);

                await RunAsyncWriterVariants<_WriteAll>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAllAsync(enumerable);
                        }

                        var txt = await getStr();
                        Assert.Equal("Foo,Bar,Fizz,Buzz\r\nhello,123,5dc798f5-6477-4216-8567-9d17c05fa87e,1970-01-01 00:00:00Z\r\nhello,456,,1980-02-02 01:01:01Z\r\n,789,5dc798f5-6477-4216-8567-9d17c05fa87e,1990-03-03 02:02:02Z\r\n,12,,2000-04-04 03:03:03Z", txt);
                    }
                );
            }
        }

        [Fact]
        public async Task EmitDefaultValueAsync()
        {
            var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Never).ToOptions();

            await RunAsyncWriterVariants<_EmitDefaultValue>(
                opts,
                async (config, getWriter, getStr) =>
                {
                    await using (var writer = config.CreateAsyncWriter(getWriter()))
                    {
                        var rows =
                            new[]
                            {
                                new _EmitDefaultValue { Foo = 1, Bar = _EmitDefaultValue.E.None, Hello = _EmitDefaultValue.E.None, World = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)},
                                new _EmitDefaultValue { Foo = 0, Bar = _EmitDefaultValue.E.Fizz, Hello = null, World = default},
                            };

                        await writer.WriteAllAsync(rows);
                    }

                    var txt = await getStr();
                    Assert.Equal("1,,None,1970-01-01 00:00:00Z\r\n,Fizz,,", txt);
                }
            );
        }

        [Fact]
        public async Task EscapeLargeHeadersAsync()
        {
            // \r\n
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.CarriageReturnLineFeed).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).ToOptions();

                await RunAsyncWriterVariants<_EscapeLargeHeaders>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _EscapeLargeHeaders { A = "a", B = "b", C = "c", D = "d", E = "e", F = "f", G = "g", H = "h" });
                        }

                        var txt = await getStr();
                        Assert.Equal("\"A,bcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefgh\",\"Ij,klmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnop\",\"Qrs,tuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwx\",\"0123,4567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567\",\",,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\",\"hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world\",\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\",\"fizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\"\r\na,b,c,d,e,f,g,h\r\n", txt);
                    }
                );

                // empty
                await RunAsyncWriterVariants<_EscapeLargeHeaders>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                        }

                        var txt = await getStr();
                        Assert.Equal("\"A,bcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefgh\",\"Ij,klmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnop\",\"Qrs,tuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwx\",\"0123,4567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567\",\",,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\",\"hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world\",\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\",\"fizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\"\r\n", txt);
                    }
                );
            }

            // \r
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.CarriageReturn).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).ToOptions();

                await RunAsyncWriterVariants<_EscapeLargeHeaders>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _EscapeLargeHeaders { A = "a", B = "b", C = "c", D = "d", E = "e", F = "f", G = "g", H = "h" });
                        }

                        var txt = await getStr();
                        Assert.Equal("\"A,bcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefgh\",\"Ij,klmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnop\",\"Qrs,tuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwx\",\"0123,4567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567\",\",,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\",\"hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world\",\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\",\"fizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\"\ra,b,c,d,e,f,g,h\r", txt);
                    }
                );

                // empty
                await RunAsyncWriterVariants<_EscapeLargeHeaders>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                        }

                        var txt = await getStr();
                        Assert.Equal("\"A,bcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefgh\",\"Ij,klmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnop\",\"Qrs,tuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwx\",\"0123,4567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567\",\",,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\",\"hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world\",\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\",\"fizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\"\r", txt);
                    }
                );
            }

            // \n
            {
                var opts = Options.CreateBuilder(Options.Default).WithWriteHeader(WriteHeader.Always).WithRowEnding(RowEnding.LineFeed).WithWriteTrailingRowEnding(WriteTrailingRowEnding.Always).ToOptions();

                await RunAsyncWriterVariants<_EscapeLargeHeaders>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                            await writer.WriteAsync(new _EscapeLargeHeaders { A = "a", B = "b", C = "c", D = "d", E = "e", F = "f", G = "g", H = "h" });
                        }

                        var txt = await getStr();
                        Assert.Equal("\"A,bcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefgh\",\"Ij,klmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnop\",\"Qrs,tuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwx\",\"0123,4567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567\",\",,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\",\"hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world\",\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\",\"fizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\"\na,b,c,d,e,f,g,h\n", txt);
                    }
                );

                // empty
                await RunAsyncWriterVariants<_EscapeLargeHeaders>(
                    opts,
                    async (config, getWriter, getStr) =>
                    {
                        await using (var writer = config.CreateAsyncWriter(getWriter()))
                        {
                        }

                        var txt = await getStr();
                        Assert.Equal("\"A,bcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefghAbcdefgh\",\"Ij,klmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnopIjklmnop\",\"Qrs,tuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwxQrstuvwx\",\"0123,4567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567012345670123456701234567\",\",,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\",\"hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world hello\"\"world\",\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\"\"foo,bar\",\"fizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\r\nfizz\nbuzz\rbazz\"\n", txt);
                    }
                );
            }
        }
    }
#pragma warning restore IDE1006
}