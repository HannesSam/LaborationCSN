using System;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Web.Hosting;
using System.Web.Mvc;
using System.Xml.Linq;

namespace LaborationCSN.Controllers
{
    public class CSNController : Controller
    {
        SQLiteConnection sqlite;

        public CSNController()
        {
            string path = HostingEnvironment.MapPath("/db/");
            sqlite = new SQLiteConnection($@"DataSource={path}\csn.sqlite");

        }
        XElement SQLResult(string query, string root, string nodeName)
        {
            sqlite.Open();

            var adapt = new SQLiteDataAdapter(query, sqlite);
            var ds = new DataSet(root);
            adapt.Fill(ds, nodeName);
            XElement xe = XElement.Parse(ds.GetXml());

            sqlite.Close();
            return xe;
        }


        //
        // GET: /Csn/Test
        // 
        // Testmetod som visar på hur ni kan arbeta från SQL till XML till
        // presentations-xml som sedan används i vyn.
        // Lite överkomplicerat för just detta enkla fall men visar på idén.
        public ActionResult Test()
        {
            string query = @"SELECT a.Arendenummer, s.Beskrivning, SUM(((Sluttid-starttid +1) * b.Belopp)) as Summa
                            FROM Arende a, Belopp b, BeviljadTid bt, BeviljadTid_Belopp btb, Stodform s, Beloppstyp blt
                            WHERE a.Arendenummer = bt.Arendenummer AND s.Stodformskod = a.Stodformskod
                            AND btb.BeloppID = b.BeloppID AND btb.BeviljadTidID = bt.BeviljadTidID AND b.Beloppstypkod = blt.Beloppstypkod AND b.BeloppID LIKE '%2009'
							Group by a.Arendenummer
							Order by a.Arendenummer ASC";
            XElement test = SQLResult(query, "BeviljadeTider2009", "BeviljadTid");
            XElement summa = new XElement("Total",
                (from b in test.Descendants("Summa")
                 select (int)b).Sum());
            test.Add(summa);

            // skicka presentations xml:n till vyn /Views/Csn/Test,
            // i vyn kommer vi åt den genom variabeln "Model"
            return View(test);
        }

        //
        // GET: /Csn/Index

        public ActionResult Index()
        {
            return View();
        }


        //
        // GET: /Csn/Uppgift1

        public ActionResult Uppgift1()
        {
            string query = @"SELECT a.Arendenummer, s.Beskrivning
                            FROM Arende a, Utbetalningsplan up, Stodform s
                            WHERE a.Arendenummer = up.Arendenummer AND s.Stodformskod = a.Stodformskod
                            ORDER BY a.Arendenummer ASC";
            XElement baseNode = SQLResult(query, "Ärenden", "Ärende");

            XElement uppgift1 = new XElement("Ärenden");
            foreach (var ärende in baseNode.Elements("Ärende"))
            {
                string ärendeNummer = ärende.Element("Arendenummer").Value;
                query = @"SELECT ut.UtbetDatum as Datum, ut.UtbetStatus as Status, SUM((Sluttid-Starttid + 1) * b.Belopp) as Summa
                            FROM Utbetalningsplan up, Utbetalning ut, UtbetaldTid_Belopp utb, UtbetaldTid utid, Belopp b
                            WHERE " + ärendeNummer + @"= up.Arendenummer AND ut.UtbetPlanID = up.UtbetPlanID AND ut.UtbetID = utid.UtbetID AND utb.BeloppID = b.BeloppID AND utid.UtbetTidID = utb.UtbetaldTidID 
							GROUP BY ut.UtbetID";

                XElement secondNode = SQLResult(query, "Ärende", "Utbetalningsinfo");
                secondNode.SetAttributeValue("ÄrendeNr", ärendeNummer);
                secondNode.SetAttributeValue("Beskrivning", ärende.Element("Beskrivning").Value);


                XElement totalSumma = new XElement("TotalSumma",
                (from s in secondNode.Descendants("Summa")
                 select (int)s).Sum());

                XElement utbetaldSumma = new XElement("UtbetaldSumma",
                (secondNode.Elements().Where(b => b.Element("Status").Value == "Utbetald").Elements("Summa").Select(s => (int)s).Sum()));

                XElement kvarvarandeSumma = new XElement("KvarvarandeSumma",
                    ((int)totalSumma - (int)utbetaldSumma));

                secondNode.Add(totalSumma);
                secondNode.Add(utbetaldSumma);
                secondNode.Add(kvarvarandeSumma);

                uppgift1.Add(secondNode);
            }

            return View(uppgift1);
        }
        //
        // GET: /Csn/Uppgift2

        public ActionResult Uppgift2()
        {

            string query = @"SELECT ut.UtbetDatum as Datum, SUM((Sluttid-Starttid + 1) * b.Belopp) as Totalsumma
                            FROM Utbetalning ut, UtbetaldTid_Belopp utid_b, UtbetaldTid utid, Belopp b
                            WHERE ut.UtbetID = utid.UtbetID AND utid_b.BeloppID = b.BeloppID AND utid.UtbetTidID = utid_b.UtbetaldTidID AND ut.UtbetStatus = 'Utbetald'
                            GROUP BY ut.UtbetDatum";

            XElement uppgift2 = SQLResult(query, "UtbetalningarOchBidragstyper", "Utbetalningar");

            foreach (var utbetalning in uppgift2.Elements())
            {
                query = @"SELECT bt.Beskrivning, SUM(((Sluttid-Starttid + 1) * b.Belopp)) as Summa
                            FROM Utbetalning ut, UtbetaldTid_Belopp utid_b, UtbetaldTid utid, Belopp b, Beloppstyp bt
                            WHERE ut.UtbetDatum = " + utbetalning.Element("Datum").Value + @" AND ut.UtbetID = utid.UtbetID AND utid_b.BeloppID = b.BeloppID AND utid.UtbetTidID = utid_b.UtbetaldTidID AND ut.UtbetStatus = 'Utbetald' AND b.Beloppstypkod = bt.Beloppstypkod
							GROUP BY ut.UtbetDatum, b.Beloppstypkod";

                XElement utbetalningsTyper = SQLResult(query, "UtbetalningsTyper", "Typ");
                utbetalning.Add(utbetalningsTyper);

            }
            return View(uppgift2);
        }

        //
        // GET: /Csn/Uppgift3

        public ActionResult Uppgift3()
        {
            string query = @"SELECT btid.Starttid as Startdatum, btid.Sluttid as Slutdatum, s.Beskrivning as Typ, SUM(((Sluttid-Starttid + 1)* b.Belopp))/4 as Summa
                            FROM Arende a, Belopp b, Beloppstyp btyp, BeviljadTid btid, BeviljadTid_Belopp btid_b, Person p, Stodform s
                            WHERE a.Personnr = p.Personnr AND btid.Arendenummer = a.Arendenummer AND a.Stodformskod = s.Stodformskod AND btid_b.BeloppID = b.BeloppID AND btid_b.BeviljadTidID = btid.BeviljadTidID 
                            GROUP BY btid_b.BeviljadTidID
                            ORDER BY s.Beskrivning ASC";
            XElement uppgift3 = SQLResult(query, "BeviljadeTider", "BeviljadTid");
            return View(uppgift3);
        }
    }
}