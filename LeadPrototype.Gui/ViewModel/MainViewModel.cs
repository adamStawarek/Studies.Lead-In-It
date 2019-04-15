using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using LeadPrototype.Libs;
using LeadPrototype.Libs.Models;
using LeadPrototype.Libs.Readers;
using LeadPrototype.Libs.Readers.Settings;
using Microsoft.Win32;
using ReportGenerator.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ReportGenerator.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        public Table CorrelationTable { get; set; }
        public Table SubstituesTable { get; set; }

        private Visibility _spinnerVisibility = Visibility.Hidden;
        public Visibility SpinnerVisibility
        {
            get => _spinnerVisibility;
            set
            {
                _spinnerVisibility = value;
                RaisePropertyChanged("SpinnerVisibility");
            }
        }
        public List<Product> Products { get; set; }
        public List<Category> Categories { get; set; }
        public ObservableCollection<Packet> Packets { get; set; }
        public PriceRange PriceRange { get; set; } = new PriceRange();

        #region constraints
        private bool _categoryConstraint;
        public bool CategoryConstraint
        {
            get => _categoryConstraint;
            set
            {
                _categoryConstraint = value;
                RaisePropertyChanged("CategoryConstraint");
            }
        }

        private bool _priceConstraint;
        public bool PriceConstraint
        {
            get => _priceConstraint;
            set
            {
                _priceConstraint = value;
                RaisePropertyChanged("PriceConstraint");
            }
        }
        #endregion

        public RelayCommand OpenFileCommand { get; }
        public RelayCommand<object> DropFileCommand { get; }
        public RelayCommand GeneratePacketsCommand { get; set; }

        public MainViewModel()
        {
            OpenFileCommand = new RelayCommand(OpenFiles);
            DropFileCommand = new RelayCommand<object>(DropFiles);
            Packets = new ObservableCollection<Packet>();
            GeneratePacketsCommand = new RelayCommand(GeneratePackets);
            FetchProductsAndCategories();
        }

        private void GeneratePackets()
        {
            var packetFactory = new PacketBuilder()
                .AddProducts(Products)
                .AddCorrelationTable(CorrelationTable as CorrelationTable);

            if (CategoryConstraint)
            {
                var selectedCategories = Categories.Where(c => c.IsSelected);
                packetFactory.AddPacketConstraint(p => selectedCategories.Any(c => c.CategoryId == p.CategoryId));
            }

            if (PriceConstraint)
            {
                packetFactory.AddPacketConstraint(p =>
                    p.AveragePrice >= PriceRange.From && p.AveragePrice <= PriceRange.To);
            }

            try
            {
                var packets = packetFactory.CreatePackets().OrderBy(p => p.prod1).ToList();
                Packets.Clear();
                foreach (var packet in packets)
                {
                    var prod1 = Products.FirstOrDefault(p => p.Id == packet.prod1);
                    var prod2 = Products.FirstOrDefault(p => p.Id == packet.prod2);
                    Packets.Add(new Packet()
                    {
                        Products = new[] { prod1, prod2 },
                        Value = packet.val
                    });
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Something wrong: {e.InnerException}");
            }
            
        }

        private void FetchProductsAndCategories()
        {
            var settings = new CsvSettings(@"../../../Tmp/products.csv", "");
            var reader = ReaderFactory.CreateReader(settings);
            Products = reader.ReadObject().ToList();
            Categories = Products.GroupBy(x => new { x.CategoryId, x.CategoryName }).Select(p => new Category()
            {
                CategoryId = p.Key.CategoryId,
                CategoryName = p.Key.CategoryName
            }).ToList();
        }

        private async void DropFiles(object p)
        {
            var e = p as DragEventArgs;
            if (e != null && !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files == null) return;
            if(files.Length<1)
                throw new Exception("No file chosen");
            else if (files.Length==1)
            {
                if(CorrelationTable==null)
                    CorrelationTable = await FetchTable(files[0],TableType.Correlation);
                else
                {
                    SubstituesTable = await FetchTable(files[0], TableType.Substitutes);
                }
            }
            else if (files.Length >= 1)
            {
                CorrelationTable = await FetchTable(files[0], TableType.Correlation);
                SubstituesTable = await FetchTable(files[1], TableType.Substitutes);
            }           
        }

        private async void OpenFiles()
        {
            var openFileDialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Csv file|*.csv;"
            };
            if (openFileDialog.ShowDialog() != true) return;

            var files = openFileDialog.FileNames;
            if (files.Length < 1)
                throw new Exception("No file chosen");
            else if (files.Length == 1)
            {
                if (CorrelationTable == null)
                    CorrelationTable = await FetchTable(files[0], TableType.Correlation);
                else
                {
                    SubstituesTable = await FetchTable(files[0], TableType.Substitutes);
                }
            }
            else if (files.Length >= 1)
            {
                CorrelationTable = await FetchTable(files[0], TableType.Correlation);
                SubstituesTable = await FetchTable(files[1], TableType.Substitutes);
            }
        }

        private async Task<Table> FetchTable(string file,TableType type)
        {
            Table tmpTable=null;

            SpinnerVisibility = Visibility.Visible;
            await Task.Run(delegate
            {
                try
                {
                    var settings = new CsvSettings("", file);
                    var reader = ReaderFactory.CreateReader(settings);

                    tmpTable = reader.ReadTable(type);
                    Dispatcher.CurrentDispatcher.Invoke(() =>
                    {
                        SpinnerVisibility = Visibility.Hidden;
                        MessageBox.Show($"Sucesfully loadded table from {file.Split('\\').Last()}, number of rows: {tmpTable.Content.Count}");
                    });
                   
                }
                catch (Exception e)
                {
                    Dispatcher.CurrentDispatcher.Invoke(() =>
                    {
                        SpinnerVisibility = Visibility.Hidden;
                        MessageBox.Show($"Exception occured when reading {file.Split('\\').Last()}, exception: {e.InnerException}");
                    });
                    
                }
            });
            return tmpTable;
        }
    }
}