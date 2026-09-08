using System;
using System.Collections.Generic;
using Elemental.Presentation.UI;
namespace Elemental.Authoring.Editor
{
    public static class MenuLayoutNames
    {
        private static readonly Dictionary<string,string> Names=new()
        {
            {"Root","Весь блок страницы"},{"Buttons","Все кнопки вместе"},{"round-result","Весь экран результата"},
            {"ROOM CODE","Заголовок «Код комнаты»"},{"ENTER ROOM CODE","Заголовок «Введите код комнаты»"},
            {"Room code input","Поле ввода кода комнаты"},{"Text viewport","Область текста"},
            {"PASTE CODE","Подсказка «Вставьте код»"},{"PAUSED","Заголовок «Пауза»"},
            {"\u00e2\u20ac\u201d","Значение кода комнаты"},
            {"Stone curtain","Тёмная боковая подложка"},{"Menu column","Вся боковая панель"},
            {"Elemental logo","Цветной логотип"},{"Stone wordmark","Название EL EMENTAL"},{"LOCAL ALPHA  /  01","Подпись версии"},
            {"Element status block","Блок выбора стихии"},{"Active element ribbon","Карточка активной стихии"},
            {"Active element glyph","Большая эмблема стихии"},{"Element glyph","Эмблема"},{"Element footer","Нижняя строка стихий"},
            {"Element footer rule","Нижняя разделительная линия"},{"Selected element footer marker","Маркер выбранной стихии"},
            {"Selected footer segment","Подсветка выбранной стихии"},{"Element footer diamond","Ромб маркера"},
            {"PLAY VS BOT","Кнопка «Играть с ботом»"},{"HOST GAME","Кнопка «Создать игру»"},{"JOIN GAME","Кнопка «Войти в игру»"},
            {"SETTINGS","Кнопка «Настройки»"},{"QUIT","Кнопка «Выйти»"},{"BACK","Кнопка «Назад»"},
            {"RESUME","Кнопка «Продолжить»"},{"END MATCH - MAIN MENU","Кнопка «Завершить матч»"},
            {"COPY CODE","Кнопка «Копировать код»"},{"READY","Кнопка «Готов»"},{"CONNECT","Кнопка «Подключиться»"},
            {"MASTER VOLUME","Общая громкость"},{"UI VOLUME","Громкость интерфейса"},{"CAMERA SENSITIVITY","Чувствительность камеры"},
            {"Reduced motion","Переключатель уменьшения анимации"},{"REDUCED MOTION","Подпись уменьшения анимации"},
            {"reference-result-title","Заголовок победы / поражения"},{"reference-result-emblem","Большая эмблема результата"},
            {"reference-result-divider","Разделитель над счётом"},{"reference-result-score","Счёт матча"},
            {"restart-round","Кнопка «Реванш / Повторить»"},{"reference-result-menu","Кнопка «В главное меню»"},
            {"reference-result-scrim","Затемнение фона"},{"reference-result-atmosphere","Свечение и частицы эмблемы"},
            {"reference-result-motion","Общая группа оформления"},{"reference-result-logo","Цветной логотип"},
            {"reference-result-brand","Название игры"},{"reference-result-halo","Ореол эмблемы"},{"reference-result-motto","Нижняя фраза"},
            {"reference-button-visual","Внешний вид кнопки"},{"reference-button-artwork","Спрайт подложки кнопки"},
            {"reference-result-button-icon","Значок кнопки"},{"reference-result-button-arrow","Стрелка кнопки"},
            {"reference-button-caption","Текст кнопки"},{"Countdown","Обратный отсчёт"},{"Returning","Блок возвращения"},
            {"Track","Дорожка ползунка"},{"Fill","Заполнение"},{"Handle area","Область движения ручки"},
            {"Handle","Ручка ползунка"},{"Handle light core","Свечение ручки"},{"Box","Рамка флажка"},
            {"Unchecked interior","Фон флажка"},{"Mark","Галочка"},{"Glow","Свечение"},
            {"EARTH","Земля"},{"FIRE","Огонь"},{"WATER","Вода"},{"AIR","Воздух"}
        };
        public static string Friendly(string path,MenuScreenId screen)
        {
            if(path=="Root"&&screen==MenuScreenId.Sidebar)return "Всё боковое меню целиком";
            if(path=="Room code input/Text viewport/")return "Поле ввода кода комнаты › Введённый текст";
            if(Names.TryGetValue(path,out var exact))return exact;
            if(path.StartsWith("Menu column/SHAPE",StringComparison.Ordinal))return "Слоган под названием игры";
            if(path=="EARTH  /  FIRE  /  WATER  /  AIR")return "Строка названий стихий";
            var labels=new List<string>();
            foreach(var part in path.Split('/'))
            {
                if(part=="Menu column"||part=="round-result"||part=="Buttons")continue;
                string label=part.Trim();
                if(Names.TryGetValue(label,out var name))label=name;
                else label=label.Replace("Element token ","Карточка: ").Replace("Element status ","Индикатор: ")
                    .Replace(" icon"," — значок").Replace(" value"," — значение").Replace(" slider"," — ползунок")
                    .Replace("MASTER VOLUME","Общая громкость").Replace("UI VOLUME","Громкость интерфейса")
                    .Replace("CAMERA SENSITIVITY","Чувствительность камеры").Replace("Earth","Земля")
                    .Replace("Fire","Огонь").Replace("Water","Вода").Replace("Air","Воздух");
                if(label.Length==0)label="Декоративный разделитель";labels.Add(label);
            }
            return string.Join(" › ",labels);
        }
    }
}
