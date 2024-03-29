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
using MoreLinq;

namespace ReportGenerator.ViewModel
{
    public class MainViewModel : ViewModelBase
    {

        #region fields
        private int substitutesCount = 5;
        private const int batchCount = 5;
        #endregion

        #region properties        
        public bool EncryptProperties { get; set; }
        public CorrelationTable CorrelationTable { get; set; }
        public SubstitutesTable SubstituesTable { get; set; }
        public List<Product> Products { get; set; }
        public ObservableCollection<FileViewModel> Files { get; set; }
        public ObservableCollection<Packet> Packets { get; set; }
        #endregion

        #region computed properties
        private Packet _selectedPacket;
        public Packet SelectedPacket
        {
            get => _selectedPacket;
            set
            {
                _selectedPacket = value;
                RaisePropertyChanged(nameof(SelectedPacket));
            }
        }
        private Visibility _spinnerVisibility;
        public Visibility SpinnerVisibility
        {
            get => _spinnerVisibility;
            set
            {
                _spinnerVisibility = value;
                RaisePropertyChanged(nameof(SpinnerVisibility));
            }
        }
        private float[] _classesBounds;
        public float[] ClassesBounds
        {
            get => _classesBounds;
            set
            {
                _classesBounds = value;
                RaisePropertyChanged(nameof(ClassesBounds));
            }
        }
        private List<Product> _packetsProducts;
        public List<Product> PacketsProducts
        {
            get => _packetsProducts;
            set
            {
                _packetsProducts = value;
                RaisePropertyChanged(nameof(PacketsProducts));
            }
        }        
        #endregion

        #region constraints 
        public List<CategoryViewModel> Categories { get; set; }
        private bool _categoryConstraint;
        public bool CategoryConstraint
        {
            get => _categoryConstraint;
            set
            {
                _categoryConstraint = value;
                RaisePropertyChanged(nameof(CategoryConstraint));
            }
        }

        public PriceRange PriceRange { get; set; }
        private bool _priceConstraint;
        public bool PriceConstraint
        {
            get => _priceConstraint;
            set
            {
                _priceConstraint = value;
                RaisePropertyChanged(nameof(PriceConstraint));
            }
        }

        public string ProductName { get; set; }
        private bool _productNameConstraint;
        public bool ProductNameConstraint
        {
            get => _productNameConstraint;
            set
            {
                _productNameConstraint = value;
                RaisePropertyChanged(nameof(ProductNameConstraint));
            }
        }

        public decimal? CorrelationMinConstraint { get; set; }
        public decimal? CorrelationMaxConstraint { get; set; }
        private bool _correlationConstraint;      
        public bool CorrelationConstraint
        {
            get => _correlationConstraint;
            set
            {
                _correlationConstraint = value;
                RaisePropertyChanged(nameof(CorrelationConstraint));
            }
        }
        #endregion

        #region commands
        public RelayCommand OpenFileCommand { get; }
        public RelayCommand<object> DropFileCommand { get; }
        public RelayCommand GeneratePacketsCommand { get; set; }
        public RelayCommand ReadFilesCommand { get; set; }
        public RelayCommand<object> SwapProductsCommand { get; set; }
        #endregion

        public MainViewModel()
        {
            OpenFileCommand = new RelayCommand(OpenFiles);
            DropFileCommand = new RelayCommand<object>(DropFiles);
            GeneratePacketsCommand = new RelayCommand(GeneratePackets);
            ReadFilesCommand = new RelayCommand(ReadFiles);
            SwapProductsCommand = new RelayCommand<object>(SwapProducts);

            Files = new ObservableCollection<FileViewModel>();
            Packets = new ObservableCollection<Packet>();
            PriceRange = new PriceRange();
            SpinnerVisibility = Visibility.Hidden;
            EncryptProperties = true;

            FetchProductsAndCategories();
        }

        private void FetchProductsAndCategories()
        {
            var settings = new CsvSettings(@"C:\Windows\LeadPrototype\products.csv", "");
            var reader = ReaderFactory.CreateReader(settings);
            Products = reader.ReadProducts().ToList();
            Categories = Products.GroupBy(x => new { x.CategoryId, x.CategoryName }).Select(p => new CategoryViewModel()
            {
                CategoryId = p.Key.CategoryId,
                CategoryName = p.Key.CategoryName
            }).ToList();
        }

        private void SwapProducts(object obj)
        {
            var values = (object[])obj;
            var newProductId = (int)values[0];
            var orginal = (PacketProduct)values[1];
            var newProduct = Products.First(p => p.Id == newProductId);
            newProduct.EncryptProperties();
            SelectedPacket.ChangeProduct(orginal.Product, newProduct);
        }

        #region packet generation
        private async void GeneratePackets()
        {
            var packetFactory = new PacketBuilder()
                .AddProducts(Products)
                .AddCorrelationTable(CorrelationTable)
                .AddSubstitutesTable(SubstituesTable)
                .SetNumberOfSubstitutes(substitutesCount);

            AddContraints(packetFactory);

            try
            {
                PacketsProducts = null;
                Packets.Clear();
                SpinnerVisibility = Visibility.Visible;
                var packets = await Task.Run(() => packetFactory.CreatePackets());
                packets = packets.OrderByDescending(p => p.Correlation).ToList();
                packets.ForEach(p =>
                {
                    p.PacketProducts.ForEach(pp=>
                    {
                        pp.Product.EncryptProperties();
                        pp.Substitutes.ForEach(s=>s.Key.EncryptProperties());
                    });
                    Packets.Add(p);
                });
                ClassesBounds = await Task.Run(() => CreateClassesBounds());
                PacketsProducts = Packets.Select(p => p.PacketProducts[0].Product).ToList();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"cannot create packets: {e.InnerException}");
            }
            finally
            {
                SpinnerVisibility = Visibility.Hidden;
            }
        }

        private void AddContraints(PacketBuilder packetFactory)
        {
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

            if (ProductNameConstraint)
            {
                packetFactory.AddPacketConstraint(p => p.ProductName.ToLower().Contains(ProductName.ToLower()));
            }

            if (CorrelationConstraint)
            {
                packetFactory.SetCorrelationConstraint((float?)CorrelationMinConstraint, (float?)CorrelationMaxConstraint);
            }
        }

        private float[] CreateClassesBounds()
        {
            long correlationSum = Packets.Sum(p => (int)p.Correlation);
            long batchCorrelation = correlationSum / batchCount;
            var bounds = new List<float>();
            double sum = 0;
            foreach (var corr in Packets.Select(p => p.Correlation))
            {
                if (sum < batchCorrelation)
                    sum += corr;
                else
                {
                    bounds.Add(corr);
                    sum = 0;
                }
            }
            return bounds.ToArray();
        }
        #endregion

        #region fetching tables from files
        private void DropFiles(object eventArgs)
        {
            var e = eventArgs as DragEventArgs;
            if (e != null && !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files == null) return;
            files.ToList().ForEach(f => Files.Add(new FileViewModel(f)));
        }

        private void OpenFiles()
        {
            var openFileDialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Csv file|*.csv;"
            };
            if (openFileDialog.ShowDialog() != true) return;

            var files = openFileDialog.FileNames;
            files.ToList().ForEach(f => Files.Add(new FileViewModel(f)));

        }

        private async void ReadFiles()
        {
            Packets.Clear();
            CorrelationTable = null;
            SubstituesTable = null;
            var correlationFile = Files.First(f => f.IsCorrelationTable).FilePath;
            var substituesFile = Files.First(f => f.IsSubstitutesTable).FilePath;

            SpinnerVisibility = Visibility.Visible;

            var watch = new Stopwatch();
            watch.Start();

            var cTask = Task.Run(() => FetchTable(correlationFile, TableType.Correlation));
            var fTask = Task.Run(() => FetchTable(substituesFile, TableType.Substitutes));

            CorrelationTable = (CorrelationTable)await cTask;
            SubstituesTable = (SubstitutesTable)await fTask;

            watch.Stop();
            SpinnerVisibility = Visibility.Hidden;

            MessageBox.Show($"Correlation table size: {CorrelationTable.Content.Count}" +
                            $"{Environment.NewLine}Substitutes table size: {SubstituesTable.Content.Count}" +
                            $"{Environment.NewLine}Excecution time: {watch.ElapsedMilliseconds}(ms)", "Fetching tables finished", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private Table FetchTable(string file, TableType type)
        {
            Table tmpTable = null;
            try
            {
                var settings = new CsvSettings("", file);
                var reader = ReaderFactory.CreateReader(settings);
                tmpTable = reader.ReadTable(type);
            }
            catch (Exception e)
            {
                Dispatcher.CurrentDispatcher.Invoke(() =>
                {
                    SpinnerVisibility = Visibility.Hidden;
                    MessageBox.Show($"Exception occured when reading {file.Split('\\').Last()}, exception: {e.InnerException}");
                });

            }
            return tmpTable;
        }
        #endregion
    }
}