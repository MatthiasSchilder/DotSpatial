﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows.Forms;
using BruTile.Web.Wms;
using Exception = System.Exception;

namespace DotSpatial.Plugins.WebMap.WMS_New
{
    public partial class WMSServerParameters : Form
    {
        private WmsCapabilities _wmsCapabilities;
        public WmsInfo WmsInfo { get; private set; }

        public WMSServerParameters(WmsInfo data)
        {
            InitializeComponent();
            
            WmsInfo = data;
            if (WmsInfo == null) return;

            _wmsCapabilities = WmsInfo.WmsCapabilities;
            tbServerUrl.Text = WmsInfo.ServerUrl;

            ShowServerDetails(_wmsCapabilities);
            InitLayers(_wmsCapabilities);

            // Select layer
            tvLayers.SelectedNode = FindNodeByLayer(tvLayers.Nodes, WmsInfo.Layer);
            if (tvLayers.SelectedNode != null) tvLayers.SelectedNode.EnsureVisible();
            tvLayers.Select();
            tvLayers.Focus();

            // Select CRS
            lbCRS.SelectedItem = WmsInfo.CRS;

            // Select Style
            for (int i = 0; i < lbStyles.Items.Count; i++)
            {
                var style = (Style) lbStyles.Items[i];
                if (style.Name == WmsInfo.Style)
                {
                    lbStyles.SelectedIndex = i;
                    break;
                }
            }

            // Show custom parameters
            if (WmsInfo.CustomParameters != null)
            {
                tbCustomParameters.Text = string.Join(Environment.NewLine,
                    WmsInfo.CustomParameters.Select(d => string.Format("{0}={1}", d.Key, d.Value)));
            }
        }

        private static TreeNode FindNodeByLayer(TreeNodeCollection nodes, Layer layer)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag == layer) return node;
                var ch = FindNodeByLayer(node.Nodes, layer);
                if (ch != null) return ch;
            }
            return null;
        }

        private void btnGetCapabilities_Click(object sender, EventArgs e)
        {
            var serverUrl = tbServerUrl.Text;
            if (String.IsNullOrWhiteSpace(serverUrl)) return;

            if (serverUrl.IndexOf("Request=GetCapabilities", StringComparison.OrdinalIgnoreCase) < 0)
            {
                serverUrl = serverUrl + "&Request=GetCapabilities";
            }
            if (serverUrl.IndexOf("SERVICE=WMS", StringComparison.OrdinalIgnoreCase) < 0)
            {
                serverUrl = serverUrl + "&SERVICE=WMS";
            }

            WmsCapabilities capabilities;
            try
            {
                var myRequest = WebRequest.Create(serverUrl);
                using (var myResponse = myRequest.GetResponse())
                using (var stream = myResponse.GetResponseStream())
                    capabilities = new WmsCapabilities(stream);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to read capabilities: " + ex.Message);
                return;
            }

            WmsInfo = null;
            _wmsCapabilities = capabilities;

            ShowServerDetails(capabilities);
            InitLayers(capabilities);
        }

        private void ShowServerDetails(WmsCapabilities capabilities)
        {
            tbServerTitle.Text = capabilities.Service.Title;
            tbServerAbstract.Text = capabilities.Service.Abstract;
            tbServerOnlineResource.Text = capabilities.Service.OnlineResource.Href;
            tbServerAccessConstraints.Text = capabilities.Service.AccessConstraints;
        }

        private void InitLayers(WmsCapabilities capabilities)
        {
            tvLayers.Nodes.Clear();
            FillTree(tvLayers.Nodes, capabilities.Capability.Layer);
            tvLayers.ExpandAll();   
        }

        private static void FillTree(TreeNodeCollection collection, Layer root)
        {
            var node = new TreeNode(root.Title) { Tag = root };
            collection.Add(node);
            foreach (var childLayer in root.ChildLayers)
            {
                FillTree(node.Nodes, childLayer);
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (_wmsCapabilities == null)
            {
                MessageBox.Show("Select server to view.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (tvLayers.SelectedNode == null)
            {
                MessageBox.Show("Select layer to view.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (lbCRS.SelectedItem == null)
            {
                MessageBox.Show("Select CRS to view.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }


            // Parse custom parameters
            var cs = string.IsNullOrWhiteSpace(tbCustomParameters.Text)
                ? new Dictionary<string, string>()
                : tbCustomParameters.Text.Split(new[] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries)
                    .Select(d => d.Split('=')).ToDictionary(d => d[0], d => d[1]);

            WmsInfo = new WmsInfo(tbServerUrl.Text, _wmsCapabilities,
                (Layer) tvLayers.SelectedNode.Tag, cs, (string) lbCRS.SelectedItem,
                lbStyles.SelectedItem == null? null :
                ((Style) lbStyles.SelectedItem).Name);
            DialogResult = DialogResult.OK;
        }

        private void tvLayers_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (!e.Node.IsSelected) return;
            var layer = (Layer)e.Node.Tag;

            tbTitle.Text = layer.Title;
            tbName.Text = layer.Name;
            lblQuerable.Text = "Queryable: " + (layer.Queryable ? "Yes" : "No");
            lblOpaque.Text = "Opaque: " + (layer.Opaque ? "Yes" : "No");
            lblNoSubsets.Text = "NoSubsets: " + (layer.NoSubsets ? "Yes" : "No");
            lblCascaded.Text = "Cascaded: " + layer.Cascaded;
            tbAbstract.Text = layer.Abstract != null? layer.Abstract.Trim() : null;
            lblFixedHeight.Text = "Fixed Height: " + layer.FixedHeight;
            lblFixedWidth.Text = "Fixed Width: " + layer.FixedWidth;
            
            var node = e.Node;
            var styles = new List<Style>();
            var crss = new List<string>();
            while (node != null)
            {
                var lr = (Layer)node.Tag;
                foreach (var style in lr.Style)
                {
                    if (styles.All(d => d.Title != style.Title))
                    {
                        styles.Add(style);
                    }
                }
                foreach (var crs in lr.CRS)
                {
                    if (!crss.Contains(crs)) crss.Add(crs);
                }
                foreach (var crs in lr.SRS)
                {
                    if (!crss.Contains(crs)) crss.Add(crs);
                }
                node = node.Parent;
            }
            lbStyles.DataSource = styles;
            lbCRS.DataSource = crss;
        }
    }
}
