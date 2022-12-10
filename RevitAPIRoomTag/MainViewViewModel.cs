using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAPIRoomTag
{
    public class MainViewViewModel
    {
        private ExternalCommandData _commandData;

        public List<FamilySymbol> Tags { get; } = new List<FamilySymbol>();
        public RoomTagType SelectedTag { get; set; }
        public DelegateCommand SaveCommand { get; } //нужно для кнопки SaveCommand

        public MainViewViewModel(ExternalCommandData commandData)
        {
            _commandData = commandData;
            var doc = commandData.Application.ActiveUIDocument.Document;
            Tags = GetRoomTagTypes(commandData);
            SaveCommand = new DelegateCommand(OnSaveCommand);
        }

        private void OnSaveCommand()
        {
            UIApplication uiapp = _commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            //Список комнат в модели
            List<Room> rooms = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .Cast<Room>()
                .ToList();

            //Список существующих в модели меток
                var existingTags = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_RoomTags)
                .WhereElementIsNotElementType()
                .Cast<RoomTag>()
                .ToList();

            //Список комнат и их ElementID, у которых уже стоят метки
            List<Room> roomsWithTags = new List<Room>();
            foreach (var tag in existingTags)
            {
                Room room = tag.Room;
                roomsWithTags.Add(room);
            }

            List<ElementId> roomsWithTagsID = new List<ElementId>();
            foreach (Room room in roomsWithTags)
            {
                roomsWithTagsID.Add(room.Id);
            }

            if (rooms.Count == 0)
            {
                TaskDialog.Show("Ошибка", $"В модели нет комнат");
                return;
            }
            if (SelectedTag == null)
            {
                TaskDialog.Show("Ошибка", $"Выберите тип метки");
                return;
            }

            try
            {
                if (roomsWithTagsID.Count > 0)
                {
                    ExclusionFilter filter = new ExclusionFilter(roomsWithTagsID);
                    //Комнаты, у которых пока нет меток
                    List<Room> roomsWithoutTags = new FilteredElementCollector(doc)
                        .WherePasses(filter)
                        .OfCategory(BuiltInCategory.OST_Rooms)
                        .Cast<Room>()
                        .ToList();

                    using (var ts = new Transaction(doc, "Create room tag"))
                    {
                        ts.Start();

                        foreach (Room r in roomsWithoutTags)
                        {
                            LocationPoint locationPoint = r.Location as LocationPoint;
                            UV point = new UV(locationPoint.Point.X, locationPoint.Point.Y);
                            RoomTag newTag = doc.Create.NewRoomTag(new LinkElementId(r.Id), point, null);
                            newTag.RoomTagType = SelectedTag;
                        }
                        ts.Commit();
                    }

                    TaskDialog.Show("Результат", $"Количество комнат: {rooms.Count}{Environment.NewLine}Количество новых меток: {roomsWithoutTags.Count}");
                    RaiseCloseRequest();
                }
                else
                {
                    using (var ts = new Transaction(doc, "Create room tag"))
                    {
                        ts.Start();

                        foreach (Room r in rooms)
                        {
                            LocationPoint locationPoint = r.Location as LocationPoint;
                            UV point = new UV(locationPoint.Point.X, locationPoint.Point.Y);
                            RoomTag newTag = doc.Create.NewRoomTag(new LinkElementId(r.Id), point, null);
                            newTag.RoomTagType = SelectedTag;
                        }
                        ts.Commit();
                    }
                    TaskDialog.Show("Результат", $"Количество комнат: {rooms.Count}{Environment.NewLine}Количество новых меток: {rooms.Count}");
                    RaiseCloseRequest();
                }
            }
            catch (NullReferenceException e)
            {
                TaskDialog.Show("Ошибка", $"{e.Message}");
                return;
            }
           
        }


        public event EventHandler CloseRequest;
        private void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }

        public static List<FamilySymbol> GetRoomTagTypes(ExternalCommandData commandData)
        {
            var uiapp = commandData.Application;
            var uidoc = uiapp.ActiveUIDocument;
            var doc = uidoc.Document;

            var roomTags = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_RoomTags)
                .OfType<FamilySymbol>()
                .ToList();

            if (roomTags == null)
            {
                TaskDialog.Show("Error", "Не найдено");
            }
            return roomTags;
        }

    }
}
