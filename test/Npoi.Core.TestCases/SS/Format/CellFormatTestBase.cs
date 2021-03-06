/* ====================================================================
   Licensed to the Apache Software Foundation (ASF) under one or more
   contributor license agreements.  See the NOTICE file distributed with
   this work for Additional information regarding copyright ownership.
   The ASF licenses this file to You under the Apache License, Version 2.0
   (the "License"); you may not use this file except in compliance with
   the License.  You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
==================================================================== */
namespace TestCases.SS.Format
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Text.RegularExpressions;
	// TODO: Port
	//using System.Windows.Forms;
    using NUnit.Framework;
    using Npoi.Core.HSSF.UserModel;
    using Npoi.Core.SS.Format;
    using Npoi.Core.SS.UserModel;
    using Npoi.Core.Util;
    using TestCases.SS;
    using System.Diagnostics;

    /**
     * This class is a base class for spreadsheet-based tests, such as are used for
     * cell formatting.  This Reads tests from the spreadsheet, as well as Reading
     * flags that can be used to paramterize these tests.
     * <p/>
     * Each test has four parts: The expected result (column A), the format string
     * (column B), the value to format (column C), and a comma-Separated list of
     * categores that this test falls in1. Normally all tests are Run, but if the
     * flag "Categories" is not empty, only tests that have at least one category
     * listed in "Categories" are Run.
     */
    //[TestFixture]
    public class CellFormatTestBase
    {
        private static POILogger logger = POILogFactory.GetLogger(typeof(CellFormatTestBase));

        private ITestDataProvider _testDataProvider;

        protected IWorkbook workbook;

        private string testFile;
        private Dictionary<string, string> testFlags;
        private bool tryAllColors;
		// TODO: Port
        //private Label label;

        private static string[] COLOR_NAMES =
            {"Black", "Red", "Green", "Blue", "Yellow", "Cyan", "Magenta",
                    "White"};
        private static Color[] COLORS = { Color.Black, Color.Red, Color.Green, Color.Blue, Color.Yellow, Color.Cyan, Color.Magenta, Color.Wheat };

        public static Color TEST_COLOR = Color.Orange; //Darker();

        protected CellFormatTestBase(ITestDataProvider testDataProvider)
        {
            _testDataProvider = testDataProvider;
        }

        public abstract class CellValue
        {
            public abstract Object GetValue(ICell cell);

            public Color GetColor(ICell cell)
            {
                return TEST_COLOR;
            }

            public virtual void Equivalent(string expected, string actual, CellFormatPart format)
            {
                Assert.AreEqual('"' + expected + '"',
                        '"' + actual + '"', "format \"" + format.ToString() + "\"");
            }
        }

        protected void RunFormatTests(string workbookName, CellValue valueGetter)
        {
			OpenWorkbook(workbookName);

            ReadFlags(workbook);

            SortedList<string, object> runCategories = new SortedList<string, object>(StringComparer.OrdinalIgnoreCase);
            string RunCategoryList = flagString("Categories", "");
            Regex regex = new Regex("\\s*,\\s*");
            if (RunCategoryList != null)
            {
                foreach (string s in regex.Split(RunCategoryList))
                    if (!runCategories.ContainsKey(s))
                        runCategories.Add(s, null);
                runCategories.Remove(""); // this can be found and means nothing
            }

            ISheet sheet = workbook.GetSheet("Tests");
            int end = sheet.LastRowNum;
            // Skip the header row, therefore "+ 1"
            for (int r = sheet.FirstRowNum + 1; r <= end; r++)
            {
                IRow row = sheet.GetRow(r);
                if (row == null)
                    continue;
                int cellnum = 0;
                string expectedText = row.GetCell(cellnum).StringCellValue;
                string format = row.GetCell(1).StringCellValue;
                string testCategoryList = row.GetCell(3).StringCellValue;
                bool byCategory = RunByCategory(runCategories, testCategoryList);
                if ((expectedText.Length > 0 || format.Length > 0) && byCategory)
                {
                    ICell cell = row.GetCell(2);
                    Debug.WriteLine(string.Format("expectedText: {0}, format:{1}", expectedText, format));
                    if (format == "hh:mm:ss a/p")
                        expectedText = expectedText.ToUpper();
                    else if (format == "H:M:S.00 a/p")
                        expectedText = expectedText.ToUpper();
                    tryFormat(r, expectedText, format, valueGetter, cell);
                }
            }
        }

        /**
         * Open a given workbook.
         *
         * @param workbookName The workbook name.  This is presumed to live in the
         *                     "spreadsheets" directory under the directory named in
         *                     the Java property "POI.testdata.path".
         *
         * @throws IOException
         */
        protected void OpenWorkbook(string workbookName)
        {
            workbook = _testDataProvider.OpenSampleWorkbook(workbookName);
            workbook.MissingCellPolicy = MissingCellPolicy.CREATE_NULL_AS_BLANK;//Row.CREATE_NULL_AS_BLANK);
            testFile = workbookName;
        }

        /**
         * Read the flags from the workbook.  Flags are on the sheet named "Flags",
         * and consist of names in column A and values in column B.  These are Put
         * into a map that can be queried later.
         *
         * @param wb The workbook to look in1.
         */
        private void ReadFlags(IWorkbook wb)
        {
            ISheet flagSheet = wb.GetSheet("Flags");
            testFlags = new Dictionary<string, string>();
            if (flagSheet != null)
            {
                int end = flagSheet.LastRowNum;
                // Skip the header row, therefore "+ 1"
                for (int r = flagSheet.FirstRowNum + 1; r <= end; r++)
                {
                    IRow row = flagSheet.GetRow(r);
                    if (row == null)
                        continue;
                    string flagName = row.GetCell(0).StringCellValue;
                    string flagValue = row.GetCell(1).StringCellValue;
                    if (flagName.Length > 0)
                    {
                        testFlags.Add(flagName, flagValue);
                    }
                }
            }

            tryAllColors = flagBoolean("AllColors", true);
        }

        /**
         * Returns <tt>true</tt> if any of the categories for this run are Contained
         * in the test's listed categories.
         *
         * @param categories     The categories of tests to be Run.  If this is
         *                       empty, then all tests will be Run.
         * @param testCategories The categories that this test is in1.  This is a
         *                       comma-Separated list.  If <em>any</em> tests in
         *                       this list are in <tt>categories</tt>, the test will
         *                       be Run.
         *
         * @return <tt>true</tt> if the test should be Run.
         */
        private bool RunByCategory(SortedList<string, object> categories,
                string testCategories)
        {

            if (categories.Count == 0)
                return true;
            // If there are specified categories, find out if this has one of them
            Regex regex = new Regex("\\s*,\\s*");

            foreach (string category in regex.Split(testCategories))//.Split("\\s*,\\s*"))
            {
                if (categories.ContainsKey(category))
                {
                    return true;
                }
            }
            return false;
        }

        private void tryFormat(int row, string expectedText, string desc,
                CellValue Getter, ICell cell)
        {

            Object value = Getter.GetValue(cell);
            Color testColor = Getter.GetColor(cell);
            if (testColor == null)
                testColor = TEST_COLOR;

			// TODO: Port
			//if (label == null)
			//    label = new Label();
			//label.ForeColor = (/*setter*/testColor);
			//label.Text = (/*setter*/"xyzzy");

			logger.Log(POILogger.INFO, string.Format("Row %d: \"%s\" -> \"%s\": expected \"%s\"", row + 1,
                    value.ToString(), desc, expectedText));
            string actualText = tryColor(desc, null, Getter, value, expectedText,
                    testColor);
            logger.Log(POILogger.INFO, string.Format(", actual \"%s\")%n", actualText));

            if (tryAllColors && testColor != TEST_COLOR)
            {
                for (int i = 0; i < COLOR_NAMES.Length; i++)
                {
                    string cname = COLOR_NAMES[i];
                    tryColor(desc, cname, Getter, value, expectedText, COLORS[i]);
                }
            }
        }

        private string tryColor(string desc, string cname, CellValue Getter,
                Object value, string expectedText, Color expectedColor)
        {

			// TODO: Port
			//if (cname != null)
   //             desc = "[" + cname + "]" + desc;
   //         Color origColor = label.ForeColor;
   //         CellFormatPart format = new CellFormatPart(desc);
   //         //if (!format.Apply(label, value).Applies)
   //         //{
   //         //    // If this doesn't Apply, no color change is expected
   //         //    expectedColor = origColor;
   //         //}

   //         String actualText = label.Text;
   //         Color actualColor = label.ForeColor;
   //         Getter.Equivalent(expectedText, actualText, format);
   //         Assert.AreEqual(
   //                 expectedColor, actualColor,cname == null ? "no color" : "color " + cname);
   //         return actualText;
	        return expectedText;
        }

        /**
         * Returns the value for the given flag.  The flag has the value of
         * <tt>true</tt> if the text value is <tt>"true"</tt>, <tt>"yes"</tt>, or
         * <tt>"on"</tt> (ignoring case).
         *
         * @param flagName The name of the flag to fetch.
         * @param expected The value for the flag that is expected when the tests
         *                 are run for a full test.  If the current value is not the
         *                 expected one, you will Get a warning in the test output.
         *                 This is so that you do not accidentally leave a flag Set
         *                 to a value that prevents Running some tests, thereby
         *                 letting you accidentally release code that is not fully
         *                 tested.
         *
         * @return The value for the flag.
         */
        protected bool flagBoolean(string flagName, bool expected)
        {
            string value = testFlags[(flagName)];
            bool isSet;
            if (value == null)
                isSet = false;
            else
            {
                isSet = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals(
                        "yes", StringComparison.OrdinalIgnoreCase) || value.Equals("on", StringComparison.OrdinalIgnoreCase);
            }
            warnIfUnexpected(flagName, expected, isSet);
            return isSet;
        }

        /**
         * Returns the value for the given flag.
         *
         * @param flagName The name of the flag to fetch.
         * @param expected The value for the flag that is expected when the tests
         *                 are run for a full test.  If the current value is not the
         *                 expected one, you will Get a warning in the test output.
         *                 This is so that you do not accidentally leave a flag Set
         *                 to a value that prevents Running some tests, thereby
         *                 letting you accidentally release code that is not fully
         *                 tested.
         *
         * @return The value for the flag.
         */
        protected string flagString(string flagName, string expected)
        {
            string value = testFlags[(flagName)];
            if (value == null)
                value = "";
            warnIfUnexpected(flagName, expected, value);
            return value;
        }

        private void warnIfUnexpected(string flagName, Object expected,
                Object actual)
        {
            if (!actual.Equals(expected))
            {
                System.Console.WriteLine(
                        "WARNING: " + testFile + ": " + "Flag " + flagName +
                                " = \"" + actual + "\" [not \"" + expected + "\"]");
            }
        }
    }

}