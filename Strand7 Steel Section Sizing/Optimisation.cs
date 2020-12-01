﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Windows.Forms.VisualStyles;

namespace Strand7_Steel_Section_Sizing
{
    class Optimisation
    {
        public static void Optimise(BackgroundWorker worker, DoWorkEventArgs e)
        {
            //#####################################################################################

            SetInputs(e);
            CollectSections();
            //int nSections = D1.Count;

            bool init = true;
            string stat = "Initialising...";
            string stat2 = "opening Strand7 file...";
            string stat3 = "Optimisation started at: " + DateTime.Now + Environment.NewLine + "Optimising file: " + file;
            worker.ReportProgress(0, new object[] { stat, stat2, stat3, init });

            //#############################################
            //############## Set constants ################
            //#############################################
            #region Set constants

            //optimisation settings
            double UtilMax = 0.99;
            double DesignStress = 355;//157.9;//355/1.1;
            DampingUp = 1.0;//0.6;
            DampingDown = 1.0;//0.4;
            int iter_max = 50;
            changed = 0;

            //string builders
            StringBuilder sb = new StringBuilder(100);
            StringBuilder sb_virtual = new StringBuilder(100);

            //file paths
            string sBaseFile = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(file), System.IO.Path.GetFileNameWithoutExtension(file));
            string optFolder = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(file), "Optimisation results");
            System.IO.Directory.CreateDirectory(optFolder);
            string sOutPath = System.IO.Path.Combine(optFolder, "Section changes.txt");
            try { System.IO.File.Delete(sOutPath); }
            catch { }
            sSt7LSAPath = sBaseFile + " - optimised.LSA";
            sSt7NLAPath = sBaseFile + " - optimised.NLA";
            sSt7FreqPath = sBaseFile + " - optimised.NFA";
            string sSt7BucPath = sBaseFile + " - optimised.LBA";
            sSt7ResPath = "";
            string sSt7OptimisedPath = sBaseFile + " - optimised.st7";

            //Strand7 model properties
            int iErr;
            iErr = St7.St7Init();
            if (CheckiErr(iErr)) { return; }
            iErr = St7.St7OpenFile(1, file, System.IO.Path.GetTempPath());
            if (CheckiErr(iErr)) { return; }
            int nBeams = new int();
            int nNodes = new int();
            iErr = St7.St7GetTotal(1, St7.tyBEAM, ref nBeams);
            if (CheckiErr(iErr)) { return; }
            iErr = St7.St7GetTotal(1, St7.tyNODE, ref nNodes);
            if (CheckiErr(iErr)) { return; }
            int[] NumProperties = new int[St7.kMaxEntityTotals];
            int[] LastProperty = new int[St7.kMaxEntityTotals];
            iErr = St7.St7GetTotalProperties(1, NumProperties, LastProperty);
            if (CheckiErr(iErr)) { return; }
            int nProps = NumProperties[St7.ipBeamPropTotal]; //EDIT
            int nProps2 = LastProperty[St7.ipBeamPropTotal]; //EDIT

            beams = new Beam[nBeams];
            for (int i = 0; i < nBeams; i++)
            {
                beams[i] = new Beam(i+1);
            }

            List<int> PropExistingList = new List<int>();
            for (int ip = 0; ip < nProps; ip++)
            {
                int propIndex = ip + 1;
                int PropNum = 0;
                iErr = St7.St7GetPropertyNumByIndex(1, St7.tyBEAM, propIndex, ref PropNum);
                PropExistingList.Add(PropNum);
                if (CheckiErr(iErr)) { return; }
            }

            BeamProperty[] beamProperties = new BeamProperty[nProps2];

            for (int p = 0; p<nProps2;p++)
            {
                beamProperties[p] = new BeamProperty(p+1);
                if (iList[0].Count == 0)
                {
                    beamProperties[p].Group = 0;
                    beamProperties[p].Optimise = true;
                }
                else
                {
                    for (int g = 0; g < iList.Count; g++)
                    {
                        if (iList[g].Contains(p + 1) && PropExistingList.Contains(p + 1))
                        {
                            beamProperties[p].Group = g;
                            beamProperties[p].Optimise = true;
                        }
                    }
                }
            }

            if (worker.CancellationPending)
            {
                iErr = St7.St7CloseFile(1);
                if (CheckiErr(iErr)) { return; };
                iErr = St7.St7Release();
                if (CheckiErr(iErr)) { return; };
                e.Cancel = true;
                return;
            }          

            double[] inc = new double[nProps2];
            double[] incPrev = new double[nProps2];
            int virtual_case = 0;

            if (nProps2 < 1)
            {
                MessageBox.Show("No beams sections available. window");
                throw new Exception("No beams sections available.");
                return;
            }

            //set beams to biggest sections (to avoid instabilities)
            foreach (BeamProperty p in beamProperties)
            {
                p.CurrentSectionInt = 0;
            }

            if (worker.CancellationPending)
            {
                iErr = St7.St7CloseFile(1);
                if (CheckiErr(iErr)) { return; };
                iErr = St7.St7Release();
                if (CheckiErr(iErr)) { return; };
                e.Cancel = true;
                return;
            }

            stat2 = "setting initial sections...";
            stat3 = "";
            worker.ReportProgress(0, new object[] { stat, stat2, stat3, init });

            foreach (BeamProperty prop in beamProperties)
            {
                if (prop.Optimise)
                {
                    int p = prop.Number;
                    Section s = SecLib.Group(prop.Group)[prop.CurrentSectionInt];

                    iErr = St7.St7SetBeamSectionGeometry(1, p, s.SType, s.sectionDoubles);
                    if (CheckiErr(iErr)) { return; };

                    stat2 = "setting property " + p.ToString();
                    stat3 = "";
                    worker.ReportProgress(0, new object[] { stat, stat2, stat3, init });

                    iErr = St7.St7CalculateBeamSectionProperties(1, p, St7.btFalse, St7.btFalse);
                    if (CheckiErr(iErr)) { return; };
                }
            }
            #endregion

            if (worker.CancellationPending)
            {
                iErr = St7.St7CloseFile(1);
                if (CheckiErr(iErr)) { return; };
                iErr = St7.St7Release();
                if (CheckiErr(iErr)) { return; };
                e.Cancel = true;
                return;
            }

            #region Set up List of beams for each property
            //########################################################
            //####### Set up List of beams for each property #########
            //########################################################

            foreach (Beam b in beams)
            {
                int PropNum = 0;
                iErr = St7.St7GetElementProperty(1, St7.tyBEAM, b.Number, ref PropNum);
                if (CheckiErr(iErr)) { return; };

                double L = 0;
                iErr = St7.St7GetElementData(1, St7.tyBEAM, b.Number, ref L);
                if (CheckiErr(iErr)) { return; };

                b.PropertyNum = PropNum;
                b.Length = L;
            }
            #endregion

            StringBuilder sb_debug = new StringBuilder();

            if (worker.CancellationPending)
            {
                iErr = St7.St7CloseFile(1);
                if (CheckiErr(iErr)) { return; };
                iErr = St7.St7Release();
                if (CheckiErr(iErr)) { return; };
                e.Cancel = true;
                return;
            }

            int[] units = new int[] { St7.luMILLIMETRE, St7.fuNEWTON, St7.suMEGAPASCAL, St7.muKILOGRAM, St7.tuCELSIUS, St7.euJOULE };
            iErr = St7.St7ConvertUnits(1, units);
            if (CheckiErr(iErr)) { return; };
            iErr = St7.St7SaveFileTo(1, optFolder + @"\iter 0.st7");
            if (CheckiErr(iErr)) { return; };

            //initial solve and open results file
            stat2 = "running solver...";
            stat3 = "";
            worker.ReportProgress(0, new object[] { stat, stat2, stat3, init });
            int NumPrimary = new int();
            int NumSecondary = new int();
            RunSolver(sCase, ref NumPrimary, ref NumSecondary);
            if (worker.CancellationPending)
            {
                iErr = St7.St7CloseResultFile(1);
                if (CheckiErr(iErr)) { return; };
                iErr = St7.St7CloseFile(1);
                if (CheckiErr(iErr)) { return; };
                iErr = St7.St7Release();
                if (CheckiErr(iErr)) { return; };
                e.Cancel = true;
                return;
            }
            iErr = St7.St7CloseResultFile(1);
            if (CheckiErr(iErr)) { return; };

            //set up load cases
            if (ResList_stress.Count == 0)
            {
                for (int i = 1; i < NumPrimary + NumSecondary; i++) ResList_stress.Add(i);
            }
            if (ResList_def.Count == 0)
            {
                for (int i = 1; i < NumPrimary + NumSecondary; i++) ResList_def.Add(i);
            }
            if (optDeflections)
            {
                //add 1 to cases as virtual load case will be created
                for (int i = 0; i < ResList_stress.Count; i++)
                {
                    if (ResList_stress[i] > NumPrimary)
                    { ResList_stress[i]++; }
                }
                for (int i = 0; i < ResList_def.Count; i++)
                {
                    if (ResList_def[i] > NumPrimary)
                    { ResList_def[i]++; }
                }
                iErr = St7.St7NewLoadCase(1, "Virtual Load");
                if (CheckiErr(iErr)) { return; };
                iErr = St7.St7GetNumLoadCase(1, ref virtual_case);
                if (CheckiErr(iErr)) { return; };
                iErr = St7.St7EnableLoadCase(1, virtual_case);
                if (CheckiErr(iErr)) { return; };
            }

            init = false;
            bool stress_satisfied = true;
            bool deflections_satisfied = true;
            bool[] def_governed = new bool[nBeams];

            //####################################
            //############## Loop ################
            //####################################
            for (int iter = 1; iter < iter_max; iter++)
            {
                DampingDown = 1.0;
                int changes = 0;

                string sOutPathVirtualStresses = System.IO.Path.Combine(optFolder, "virtual stresses" + iter.ToString() + ".txt");
                sb_virtual.Append("TITLE Virtual stresses\n");
                looping = true;
                stat = "Iteration: " + iter.ToString();
                stat2 = "";
                stat3 = Environment.NewLine + "ITERATION: " + iter.ToString(); ;
                if (changed > 0) { stat2 = changed.ToString() + " changes in previous iteration"; }
                worker.ReportProgress(0, new object[] { stat, stat2, stat3, init });

                //###################################
                //####### Optimise Stresses #########
                //###################################
                ///optimise stresses loop
                ///     solve
                ///     collect results
                ///     choose sections
                ///     update sections
                ///     iterate
                if (optStresses)
                {
                    for (int iter_stress = 1; iter_stress < 50; iter_stress++)
                    {
                        foreach (Beam b in beams)
                        {
                            b.A_x_stress = 0;
                            b.M_11_stress = 0;
                            b.M_22_stress = 0;
                        }
                        if (iter_stress > 12) 
                        { DampingDown = 0.0; }
                        int changes_stress = 0;
                        stress_satisfied = true;

                        //reset unconstrained sections
                        foreach (BeamProperty p in beamProperties)
                        {
                            if (!p.DeflectionGoverned) p.NewSectionInt = 0;
                        }

                        //solve and open results file
                        stat2 = "running solver...";
                        stat3 = "";
                        worker.ReportProgress(0, new object[] { stat, stat2, stat3, init });
                        RunSolver(sCase, ref NumPrimary, ref NumSecondary);
                        if (worker.CancellationPending)
                        {
                            iErr = St7.St7CloseResultFile(1);
                            if (CheckiErr(iErr)) { return; };
                            iErr = St7.St7CloseFile(1);
                            if (CheckiErr(iErr)) { return; };
                            iErr = St7.St7Release();
                            if (CheckiErr(iErr)) { return; };
                            e.Cancel = true;
                            return;
                        }

                        //collect results
                        stat2 = "collecting results...";
                        stat3 = "";
                        worker.ReportProgress(0, new object[] { stat, stat2, stat3, init });
                        foreach (int ResCase in ResList_stress)
                        {
                            stat2 = "collecting stress results for case no " + ResCase.ToString() + "...";
                            stat3 = "";
                            worker.ReportProgress(0, new object[] { stat, stat2, stat3, init });

                            foreach (Beam b in beams)
                            {
                                int NumPoints = 0;
                                int NumColumns = 0;
                                double[] BeamPos = new double[St7.kMaxBeamResult];
                                double[] BeamResults = new double[St7.kMaxBeamResult];
                                iErr = St7.St7GetBeamResultArray(1, St7.rtBeamForce, St7.stBeamLocal, b.Number, 1, ResCase, ref NumPoints, ref NumColumns, BeamPos, BeamResults);
                                if (CheckiErr(iErr)) { return; }
                                double A_x_max = 0;
                                double M_11_max = 0;
                                double M_22_max = 0;

                                for (int j = 0; j < NumPoints; j++)
                                {
                                    double A_x_max_j = BeamResults[j * NumColumns + St7.ipBeamAxialF];
                                    double M_11_max_j = Math.Abs(BeamResults[j * NumColumns + St7.ipBeamBM2]);
                                    double M_22_max_j = Math.Abs(BeamResults[j * NumColumns + St7.ipBeamBM1]);
                                    if (Math.Abs(A_x_max_j) > Math.Abs(A_x_max)) { A_x_max = A_x_max_j; }
                                    if (M_11_max_j > M_11_max) { M_11_max = M_11_max_j; }
                                    if (M_22_max_j > M_22_max) { M_22_max = M_22_max_j; }
                                }

                                if (Math.Abs(A_x_max) > Math.Abs(b.A_x_stress)) { b.A_x_stress = A_x_max; }
                                if (M_11_max > b.M_11_stress) { b.M_11_stress = M_11_max; }
                                if (M_22_max > b.M_11_stress) { b.M_11_stress = M_22_max; }
                            }
                        }

                        iErr = St7.St7CloseResultFile(1);
                        if (CheckiErr(iErr)) { return; };
                        if (worker.CancellationPending)
                        {
                            iErr = St7.St7CloseFile(1);
                            if (CheckiErr(iErr)) { return; };
                            iErr = St7.St7Release();
                            if (CheckiErr(iErr)) { return; };
                            e.Cancel = true;
                            return;
                        }

                        //choose sections
                        stat2 = "choosing sections...";
                        stat3 = "";
                        worker.ReportProgress(0, new object[] { stat, stat2, stat3, init });

                        foreach (Beam b in beams)
                        {
                            if (beamProperties[b.PropertyNum - 1].Optimise)
                            {
                                int p = b.PropertyNum - 1;
                                int g = beamProperties[p].Group;
                                int iCurrent = beamProperties[p].NewSectionInt;

                                for (int s = iCurrent; s < SecLib.Group(g).Count; s++)
                                {
                                    double stress = b.CalcStress(SecLib.Group(g)[s]);

                                    if (stress < UtilMax * DesignStress)
                                    {
                                        beamProperties[p].NewSectionInt = s;
                                        break;
                                    }
                                    else if (s == (SecLib.Group(g).Count - 1))
                                    {
                                        beamProperties[p].NewSectionInt = s;
                                        if (stress > UtilMax * DesignStress) stress_satisfied = false;
                                        break;
                                    }
                                }
                            }
                        }

                        //sb_debug.Clear();
                        //foreach (BeamProperty p in beamProperties)
                        //{ sb_debug.Append(p.NewSectionInt + " "); }
                        //stat3 = sb_debug.ToString();
                        //worker.ReportProgress(0, new object[] { stat, stat2, stat3, init });

                        //update sections
                        UpdateSections(beamProperties, incPrev, inc, ref changes_stress);

                        if (changes_stress > 0) { stat2 = String.Format("stress iteration {0}: {1} section changes",iter_stress, changes_stress); }
                        else { stat2 = stat2 = String.Format("stress iteration {0}: sizing for stresses converged", iter_stress); }
                        stat3 = stat2;
                        worker.ReportProgress(0, new object[] { stat, stat2, stat3, init });

                        iErr = St7.St7SaveFileTo(1, optFolder + @"\iter " + iter.ToString() +" - stress "+ iter_stress.ToString() + ".st7");
                        if (CheckiErr(iErr)) { return; };

                        changes += changes_stress;
                        if (changes_stress == 0) { break; }
                    }
                }

                changes = 0;

                //######################################
                //####### Optimise Deflections #########
                //######################################
                //optimise deflections loop
                ///     collect deflection results
                ///     apply unit force
                ///     solve
                ///     choose sections
                ///     update sections
                ///     iterate
                if (optDeflections)
                {
                    for (int iter_def = 1; iter_def < 50; iter_def++)
                    {
                        int changes_def = 0;

                        //solve and open results file
                        stat2 = "running solver...";
                        stat3 = "";
                        worker.ReportProgress(0, new object[] { stat, stat2, stat3, init });
                        RunSolver(sCase, ref NumPrimary, ref NumSecondary);
                        if (worker.CancellationPending)
                        {
                            iErr = St7.St7CloseResultFile(1);
                            if (CheckiErr(iErr)) { return; };
                            iErr = St7.St7CloseFile(1);
                            if (CheckiErr(iErr)) { return; };
                            iErr = St7.St7Release();
                            if (CheckiErr(iErr)) { return; };
                            e.Cancel = true;
                            return;
                        }

                        //collect worst case deflection
                        stat2 = "applying virtual load...";
                        stat3 = "";
                        worker.ReportProgress(0, new object[] { stat, stat2, stat3, init });
                        double def_max = 0;
                        int def_node = 0;
                        int def_max_case = 0;
                        double[] virtual_load = new double[3];

                        foreach (int ResCase in ResList_def)
                        {
                            stat2 = "collecting deflection results for case no " + ResCase.ToString();
                            stat3 = "";
                            worker.ReportProgress(0, new object[] { stat, stat2, stat3, init });

                            for (int i = 0; i < nNodes; i++)
                            {
                                double[] NodeResults = new double[St7.kMaxDisp];
                                iErr = St7.St7GetNodeResult(1, St7.rtNodeDisp, i + 1, ResCase, NodeResults);
                                if (CheckiErr(iErr)) { return; };

                                double dx = NodeResults[0];
                                double dy = NodeResults[1];
                                double dz = NodeResults[2];
                                double def = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                                if (def > def_max)
                                {
                                    def_max = def;
                                    def_node = i + 1;
                                    def_max_case = ResCase;
                                    virtual_load[0] = dx / def;
                                    virtual_load[1] = dy / def;
                                    virtual_load[2] = dz / def;
                                }
                            }
                        }

                        stat2 = String.Format("max def: {0:0.0}mm", def_max);
                        stat3 = "";
                        string sDef = stat2;
                        worker.ReportProgress(0, new object[] { stat, stat2, stat3, init });
                        iErr = St7.St7CloseResultFile(1);
                        if (CheckiErr(iErr)) { return; };

                        if (def_max > def_limit)
                        {
                            //apply unit force to worst case node
                            iErr = St7.St7SetNodeForce3(1, def_node, virtual_case, virtual_load);
                            if (CheckiErr(iErr)) { return; };

                            //solve and open results file
                            stat2 = "running solver...";
                            stat3 = "";
                            worker.ReportProgress(0, new object[] { stat, stat2, stat3, init });
                            RunSolver(sCase, ref NumPrimary, ref NumSecondary);
                            if (worker.CancellationPending)
                            {
                                iErr = St7.St7CloseResultFile(1);
                                if (CheckiErr(iErr)) { return; }
                                iErr = St7.St7CloseFile(1);
                                if (CheckiErr(iErr)) { return; }
                                iErr = St7.St7Release();
                                if (CheckiErr(iErr)) { return; }
                                e.Cancel = true;
                                return;
                            }

                            //collect results
                            stat2 = "collecting virtual deflection results...";
                            stat3 = "";
                            worker.ReportProgress(0, new object[] { stat, stat2, stat3, init });

                            //reset variables
                            foreach (Beam b in beams)
                            {
                                b.A_x_def = 1;
                                b.M_11_def = 1;
                                b.M_22_def = 1;
                            }

                            List<int> cases = new List<int> { virtual_case, def_max_case };

                            foreach (Beam b in beams)
                            {
                                foreach (int c in cases)
                                {
                                    int NumPoints = 0;
                                    int NumColumns = 0;
                                    double[] BeamPos = new double[St7.kMaxBeamResult];
                                    double[] BeamResults = new double[St7.kMaxBeamResult];
                                    iErr = St7.St7GetBeamResultArray(1, St7.rtBeamForce, St7.stBeamLocal, b.Number, 8, c, ref NumPoints, ref NumColumns, BeamPos, BeamResults);
                                    if (CheckiErr(iErr)) { return; }
                                    double A_x_addition = Math.Abs(BeamResults[St7.ipBeamAxialF]);
                                    double M_11_addition = Math.Abs(BeamResults[St7.ipBeamBM2]);
                                    double M_22_addition = Math.Abs(BeamResults[St7.ipBeamBM1]);

                                    for (int j = 1; j < NumPoints; j++)
                                    {
                                        A_x_addition += Math.Abs(BeamResults[j * NumColumns + St7.ipBeamAxialF]);
                                        M_11_addition += Math.Abs(BeamResults[j * NumColumns + St7.ipBeamBM2]);
                                        M_22_addition += Math.Abs(BeamResults[j * NumColumns + St7.ipBeamBM1]);
                                    }

                                    //Multiply for sensitivity
                                    b.A_x_def *= (A_x_addition / NumPoints);
                                    b.M_11_def *= (M_11_addition / NumPoints);
                                    b.M_22_def *= (M_22_addition / NumPoints);
                                }
                            }
                            
                            iErr = St7.St7CloseResultFile(1);
                            if (CheckiErr(iErr)) { return; };
                            if (worker.CancellationPending)
                            {
                                iErr = St7.St7CloseFile(1);
                                if (CheckiErr(iErr)) { return; };
                                iErr = St7.St7Release();
                                if (CheckiErr(iErr)) { return; };
                                e.Cancel = true;
                                return;
                            }

                            //delete unit force on worst case node
                            iErr = St7.St7SetNodeForce3(1, def_node, virtual_case, new double[] { 0, 0, 0 });
                            if (CheckiErr(iErr)) { return; };

                            //choose sections
                            stat2 = "choosing sections...";
                            stat3 = "";
                            worker.ReportProgress(0, new object[] { stat, stat2, stat3, init });
                            double def_approx = def_max;

                            //foreach(BeamProperty p in beamProperties)
                            //{ p.TempSectionInt = p.CurrentSectionInt; }

                            int counter = 0;
                            
                            while (def_approx / def_limit > 0.98)
                            {
                                counter++;

                                double[] group_def_current = new double[nProps2];
                                double[] group_mass_current = new double[nProps2];
                                double[][] group_def_new = new double[nProps2][];
                                double[][] group_mass_new = new double[nProps2][];
                                double[][] group_efficiency = new double[nProps2][];
                                foreach (BeamProperty p in beamProperties)
                                {
                                    int ip = p.Number-1;
                                    int g = p.Group;
                                    int num_sections = SecLib.Group(g).Count;

                                    group_def_new[ip] = new double[num_sections];
                                    group_mass_new[ip] = new double[num_sections];
                                    group_efficiency[ip] = new double[num_sections];
                                }
                                double best_efficiency = 0;
                                int best_property=0;
                                int best_section=0;

                                //Calc prop group current deflection and mass contributions
                                foreach (Beam b in beams)
                                {
                                    int p = b.PropertyNum-1;
                                    int g = beamProperties[p].Group;
                                    int iCurrent = beamProperties[p].NewSectionInt;
                                    Section s_current = SecLib.Group(g)[iCurrent];

                                    //Calc deflections and masses per property for current properties
                                    group_def_current[p] += b.CalcDeflection(s_current);
                                    group_mass_current[p] += b.CalcMass(s_current);

                                    //Calc deflections and masses per property for all potential beams
                                    foreach (Section s in SecLib.Group(g))
                                    {
                                        group_def_new[p][s.Number] += b.CalcDeflection(s);
                                        group_mass_new[p][s.Number] += b.CalcMass(s);
                                    }
                                }

                                foreach (BeamProperty p in beamProperties)
                                {
                                    if (p.Optimise)
                                    {
                                        int g = p.Group;
                                        int ip = p.Number - 1;

                                        //Calc efficiencies
                                        foreach (Section s in SecLib.Group(g))
                                        {
                                            if (group_mass_new[ip][s.Number] - group_mass_current[ip] != 0) group_efficiency[ip][s.Number] = (group_def_current[ip] - group_def_new[ip][s.Number]) / (group_mass_new[ip][s.Number] - group_mass_current[ip]);
                                            else group_efficiency[ip][s.Number] = 0;
                                            //Choose most efficient
                                            if (group_efficiency[ip][s.Number] > best_efficiency && (group_def_new[ip][s.Number] - group_def_current[ip]) < 0)
                                            {
                                                best_efficiency = group_efficiency[ip][s.Number];
                                                best_property = ip;
                                                best_section = s.Number;
                                            }
                                        }
                                    }
                                }

                                double def_inc = (group_def_new[best_property][best_section] - group_def_current[best_property]);
                                if (def_inc>0) { break; }
                                def_approx += def_inc;
                                beamProperties[best_property].NewSectionInt = best_section;
                                beamProperties[best_property].DeflectionGoverned = true;

                                int rem = 0;
                                Math.DivRem(counter, 50, out rem);
                                if (rem == 10)
                                {
                                    stat3 = String.Format("def_approx = {0:0.00}mm, best_property = {1}, best_section = {2}", def_approx, best_property, best_section);
                                    worker.ReportProgress(0, new object[] { stat, stat2, stat3, init });
                                }

                                if (worker.CancellationPending)
                                {
                                    iErr = St7.St7CloseFile(1);
                                    if (CheckiErr(iErr)) { return; };
                                    iErr = St7.St7Release();
                                    if (CheckiErr(iErr)) { return; };
                                }
                            }

                            //update sections
                            UpdateSections(beamProperties, incPrev, inc, ref changes_def);
                        }

                        //calc current mass
                        double new_mass = 0;
                        foreach (Beam b in beams)
                        {
                            int p = b.PropertyNum-1;
                            int g = beamProperties[p].Group;
                            int iCurrent = beamProperties[p].CurrentSectionInt;
                            Section s_current = SecLib.Group(g)[iCurrent];

                            new_mass += b.CalcMass(s_current);
                        }

                        stat2 = String.Format("mass (of selection): {0:0.0}T", new_mass);
                        stat3 = "";
                        string sMass = stat2;
                        worker.ReportProgress(0, new object[] { stat, stat2, stat3, init });

                        if (changes_def > 0) { stat2 = String.Format("def iteration {0}: {1} section changes", iter_def, changes_def) +", "+ sMass + ", " + sDef; }
                        else { stat2 = String.Format("def iteration {0}: sizing for deflection converged", iter_def) + ", " + sMass + ", " + sDef; }
                        stat3 = stat2;
                        worker.ReportProgress(0, new object[] { stat, stat2, stat3, init });

                        iErr = St7.St7SaveFileTo(1, optFolder + @"\iter " + iter.ToString() + " - deflection " + iter_def.ToString() + ".st7");
                        if (CheckiErr(iErr)) { return; };

                        changes += changes_def;
                        if (def_max < def_limit) { deflections_satisfied = true; break; }
                        if (changes == 0) 
                        {
                            deflections_satisfied = false;
                            stat2 = "WARNING: Deflections cannot be further reduced, check section catalogue.";
                            stat3 = stat2;
                            worker.ReportProgress(0, new object[] { stat, stat2, stat3, init });
                            break; 
                        }
                    }
                }

                iErr = St7.St7SaveFileTo(1, optFolder + @"\iter " + iter.ToString() + ".st7");
                if (CheckiErr(iErr)) { return; };

                if (worker.CancellationPending)
                {
                    iErr = St7.St7CloseFile(1);
                    if (CheckiErr(iErr)) { return; };
                    iErr = St7.St7Release();
                    if (CheckiErr(iErr)) { return; };
                }

                if (looping) { DampingDown = 1.0; }// 0.4; }
                if (changes == 0) { break; }
            }

            //#####################################################################################

            //Set Property names:
            foreach (BeamProperty p in beamProperties)
            {
                if (p.Optimise)
                {
                    int p_num = p.Number;
                    iErr = St7.St7SetPropertyName(1, St7.tyBEAM, p_num, SecLib.Group(p.Group)[p.NewSectionInt].sName);
                    if (CheckiErr(iErr)) { return; }
                }
            }
            iErr = St7.St7DeleteLoadCase(1, virtual_case);
            if (CheckiErr(iErr)) { return; }
            iErr = St7.St7SaveFileTo(1, optFolder + @"/Optimised.st7");
            if (CheckiErr(iErr)) { return; }
            iErr = St7.St7SaveFileTo(1, sSt7OptimisedPath);
            if (CheckiErr(iErr)) { return; }
            iErr = St7.St7CloseFile(1);
            if (CheckiErr(iErr)) { return; }
            iErr = St7.St7Release();
            if (CheckiErr(iErr)) { return; }

            stat = "complete";
            stat2 = "";
            stat3 = Environment.NewLine + "Optimisation completed at: " + DateTime.Now;
            init = false;
            worker.ReportProgress(0, new object[] { stat, stat2, stat3, init });

            if (!stress_satisfied) { MessageBox.Show("Warning: One or more beams are still overstressed!"); }
            if (!deflections_satisfied) { MessageBox.Show("Warning: Deflection limits are not satisfied!"); }
            else if (changed == 0) { MessageBox.Show("Section sizing has converged!"); }
            else { MessageBox.Show("Section sizing has NOT converged. Maximum number of iterations reached."); }
        }
        private static void SetInputs(DoWorkEventArgs e)
        {
            List<object> args = (List<object>)e.Argument;
            file = (string)args[0];
            iList = (List<List<int>>)args[1];
            ResList_stress = (List<int>)args[2];
            sCase = (Solver)args[3];
            optDeflections = (bool)args[4];
            ResList_def = (List<int>)args[5];
            def_limit = (double)args[6];
            optStresses = (bool)args[7];
        }
        private static void CollectSections()
        {
            for (int g = 0; g < iList.Count; g++)
            {
                int iErr;
                string filePath = System.IO.Path.GetTempPath() + "Section_CSV" + (g+1).ToString() + ".txt";
                if (!System.IO.File.Exists(filePath))
                {
                    MessageBox.Show("section data file does not exist");
                    throw new Exception("section data file does not exist.");
                    return;
                }

                using (var fs = System.IO.File.OpenRead(filePath))
                using (var reader = new System.IO.StreamReader(fs))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        var values = line.Split(',');

                        double d1 = Convert.ToDouble(values[0]);
                        double d2 = Convert.ToDouble(values[1]);
                        double d3 = Convert.ToDouble(values[2]);
                        double t1 = Convert.ToDouble(values[3]);
                        double t2 = Convert.ToDouble(values[4]);
                        double t3 = Convert.ToDouble(values[5]);
                        double a = Convert.ToDouble(values[6]);
                        double z11 = Convert.ToDouble(values[7]);
                        double z22 = Convert.ToDouble(values[8]);
                        int stype = Convert.ToInt32(values[9]);
                        double i11 = Convert.ToDouble(values[10]);
                        double i22 = Convert.ToDouble(values[11]);

                        Section s = new Section(d1,d2,d3,t1,t2,t3,a,z11,z22,stype,i11,i22,g);
                        SecLib.AddSection(s, g);
                    }
                }

                if (SecLib.Group(g).Count == 0)
                {
                    MessageBox.Show("No section properties found.");
                    return;
                }
            }
        }
        private static void RunSolver(Solver sCase, ref int NumPrimary, ref int NumSecondary)
        {
            int iErr;
            switch (sCase)
            {
                case Solver.linear:
                    iErr = St7.St7SetResultFileName(1, sSt7LSAPath);
                    if (CheckiErr(iErr)) { return; };
                    iErr = St7.St7RunSolver(1, St7.stLinearStaticSolver, St7.smBackgroundRun, St7.btTrue);
                    if (CheckiErr(iErr)) { return; };
                    sSt7ResPath = sSt7LSAPath;
                    break;
                case Solver.nonlin:
                    iErr = St7.St7SetResultFileName(1, sSt7NLAPath);
                    if (CheckiErr(iErr)) { return; };
                    iErr = St7.St7RunSolver(1, St7.stNonlinearStaticSolver, St7.smBackgroundRun, St7.btTrue);
                    if (CheckiErr(iErr)) { return; };
                    sSt7ResPath = sSt7NLAPath;
                    break;
                case Solver.frequency:
                    iErr = St7.St7SetResultFileName(1, sSt7FreqPath);
                    if (CheckiErr(iErr)) { return; };
                    iErr = St7.St7RunSolver(1, St7.stNaturalFrequencySolver, St7.smProgressRun, St7.btTrue);
                    if (CheckiErr(iErr)) { return; };
                    sSt7ResPath = sSt7NLAPath;
                    break;

                    //sSt7ResPath = sSt7FreqPath;
                    //iErr = St7.St7OpenResultFile(1, sSt7ResPath, "", St7.btTrue, ref NumPrimary, ref NumSecondary);
                    //if (CheckiErr(iErr)) { return; };

                    //double[] ModalRes = new double[10];
                    //iErr = St7.St7GetModalResultsNFA(1, 1, ModalRes);
                    //Freq = ModalRes[0];

                    //iErr = St7.St7CloseResultFile(1);
                    //if (CheckiErr(iErr)) { return; };
                    //break;
            }

            iErr = St7.St7OpenResultFile(1, sSt7ResPath, "", St7.btTrue, ref NumPrimary, ref NumSecondary);
            if (CheckiErr(iErr)) { return; };
        }
        private static void UpdateSections(BeamProperty[] beamProperties, double[] incPrev, double[] inc, ref int changes)
        {
            int iErr = 0;
            foreach (BeamProperty p in beamProperties)
            {
                if (p.Optimise)
                {
                    int ip = p.Number - 1;

                    incPrev[ip] = inc[ip];

                    if ((p.NewSectionInt - p.CurrentSectionInt) > 0)
                    {
                        inc[ip] = (p.NewSectionInt - p.CurrentSectionInt);// * DampingUp;
                    }
                    else if ((p.NewSectionInt - p.CurrentSectionInt) < 0)
                    {
                        inc[ip] = (p.NewSectionInt - p.CurrentSectionInt) * DampingDown;
                    }
                    else inc[ip] = 0;

                    if (inc[ip] != 0)
                    {
                        p.CurrentSectionInt += Convert.ToInt32(inc[ip]);
                        int g = p.Group;
                        int s = p.CurrentSectionInt;
                        int stype = SecLib.Group(g)[s].SType;
                        double[] SectionDoubles = SecLib.Group(g)[s].sectionDoubles;

                        iErr = St7.St7SetBeamSectionGeometry(1, p.Number, stype, SectionDoubles);
                        if (CheckiErr(iErr)) { return; };
                        iErr = St7.St7CalculateBeamSectionProperties(1, p.Number, St7.btFalse, St7.btFalse);
                        if (CheckiErr(iErr)) { return; };
                        changes++;
                        if (inc[ip] != -incPrev[ip])
                        {
                            looping = false;
                        }
                    }
                    sb.Append(p.CurrentSectionInt.ToString() + ",");
                }

            }
        }
        public static double Stress(double A_x,double M_11, double M_22, double A, double I11, double I22, double L, double Z11, double Z22)
        {
            double Stress = 0;
            if (A_x < 0)
            {
                //### Buckling Check ####
                double E_s = 210000;
                double f_y = 355;
                double alpha_c = 0.49;
                double lambda = Math.Sqrt(Math.Pow(L, 2) * A / Math.Min(I11, I22));
                double N_cr = Math.Pow(Math.PI, 2) * E_s * A / Math.Pow(lambda, 2);
                double lambda_nd = Math.Max(0.2, Math.Sqrt(A * f_y / N_cr));
                double phi_m = 0.5 * (1 + alpha_c * (lambda_nd - 0.2) + Math.Pow(lambda_nd, 2));
                double chi_n = 1 / (phi_m + Math.Sqrt(Math.Pow(phi_m, 2) - Math.Pow(lambda_nd, 2)));

                Stress = Math.Abs(A_x) / A * chi_n + M_11 / Z11 + M_22 / Z22;
            }
            else
            {
                Stress = A_x / A + M_11 / Z11 + M_22 / Z22;
            }
            return Stress;
        }
        public static double Deflection(double A_x, double M_11, double M_22, double A, double I11, double I22, double L)
        {
            double Deflection = (A_x / A + M_11 / I11 + M_22 / I22) * L / 210000;
            return Deflection;
        }
        public static bool CheckiErr(int iErr)
        {
            StringBuilder sb = new StringBuilder(St7.kMaxStrLen);
            string errorstring;
            St7.St7GetAPIErrorString(iErr, sb, sb.Capacity);
            errorstring = sb.ToString();
            if (errorstring == "")
            {
                St7.St7GetSolverErrorString(iErr, sb, sb.Capacity);
                errorstring = sb.ToString();
            }
            if (errorstring != "No error.")
            {
                //MessageBox.Show(errorstring);
                //Console.WriteLine("");
                //Console.WriteLine("Strand7 API error: " + errorstring);
                //Console.WriteLine("The program has terminated early.");

                string sFilePath = System.IO.Path.GetTempPath() + "API Error Log.txt";
                System.IO.File.WriteAllText(sFilePath, errorstring);

                throw new Exception(errorstring);

                return true;
            }
            return false;
        }

        #region variables
        private static SectionLibrary SecLib = new SectionLibrary();
        private static Beam[] beams;

        //private static List<List<double>> A = new List<List<double>>();
        //private static List<List<double>> D1 = new List<List<double>>();
        //private static List<List<double>> D2 = new List<List<double>>();
        //private static List<List<double>> D3 = new List<List<double>>();
        //private static List<List<double>> T1 = new List<List<double>>();
        //private static List<List<double>> T2 = new List<List<double>>();
        //private static List<List<double>> T3 = new List<List<double>>();
        //private static List<List<double>> I11 = new List<List<double>>();
        //private static List<List<double>> I22 = new List<List<double>>();
        //private static List<List<double>> Z11 = new List<List<double>>();
        //private static List<List<double>> Z22 = new List<List<double>>();
        //private static List<List<int>> SType = new List<List<int>>();

        private static string file = "";
        private static List<List<int>> iList = new List<List<int>>();
        private static List<int> ResList_stress = new List<int>();
        private static List<int> ResList_def = new List<int>();
        private static Solver sCase = new Solver();
        private static bool optDeflections = new bool();
        private static bool optStresses = new bool();
        private static double def_limit = new double();

        private static string sSt7ResPath = "";
        private static string sSt7LSAPath = "";
        private static string sSt7NLAPath = "";
        private static string sSt7FreqPath = "";

        private static bool looping = true;
        private static int changed = 0;
        private static StringBuilder sb = new StringBuilder();
        private static double DampingUp = 1.0;
        private static double DampingDown = 1.0;
        //private static double[][][] SectionDoubles;

        public enum Solver { linear, nonlin, frequency }
        #endregion
    }
}
