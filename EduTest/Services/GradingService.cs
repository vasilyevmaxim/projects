using System.Text.Json;
using StudentTestingSystem.Models.Entities;

namespace StudentTestingSystem.Services;

public interface IGradingService
{
    double CalculateScore(Test test, Dictionary<int, string> answers, Dictionary<int, List<string>>? multiAnswers = null);
    GradingResult CalculateDetailedScore(Test test, Dictionary<int, string> answers, Dictionary<int, List<string>>? multiAnswers = null);
}

public class GradingResult
{
    public double TotalScore { get; set; }
    public int CorrectCount { get; set; }
    public int PartialCount { get; set; }
    public int WrongCount { get; set; }
    public List<QuestionGrade> QuestionGrades { get; set; } = new();
}

public class QuestionGrade
{
    public int ElementId { get; set; }
    public double PointsEarned { get; set; }
    public double MaxPoints { get; set; }
    public bool IsCorrect { get; set; }
    public bool IsPartial { get; set; }
    public string YourAnswer { get; set; } = string.Empty;
    public string CorrectAnswer { get; set; } = string.Empty;
}

public class GradingService : IGradingService
{
    public double CalculateScore(Test test, Dictionary<int, string> answers, Dictionary<int, List<string>>? multiAnswers = null)
    {
        var result = CalculateDetailedScore(test, answers, multiAnswers);
        return result.TotalScore;
    }

    public GradingResult CalculateDetailedScore(Test test, Dictionary<int, string> answers, Dictionary<int, List<string>>? multiAnswers = null)
    {
        var result = new GradingResult();
        
        if (test.TestElements == null || !test.TestElements.Any())
            return result;

        multiAnswers ??= new Dictionary<int, List<string>>();

        // Рассчитываем вес каждого вопроса
        var elements = test.TestElements.ToList();
        double totalWeight = elements.Sum(e => e.Weight ?? 1.0);
        
        foreach (var element in elements)
        {
            double weight = element.Weight ?? 1.0;
            double maxPoints = (weight / totalWeight) * test.MaxScore;
            
            var grade = GradeQuestion(element, answers, multiAnswers, maxPoints);
            result.QuestionGrades.Add(grade);
            result.TotalScore += grade.PointsEarned;
            
            if (grade.IsCorrect) result.CorrectCount++;
            else if (grade.IsPartial) result.PartialCount++;
            else result.WrongCount++;
        }

        result.TotalScore = Math.Round(result.TotalScore, 2);
        return result;
    }
    private QuestionGrade GradeTextInputBlanks(TestElement element, Dictionary<int, string> answers, double maxPoints)
    {
        var grade = new QuestionGrade
        {
            ElementId = element.Id,
            MaxPoints = maxPoints,
            CorrectAnswer = element.CorrectAnswer
        };

        // Формат правильных ответов:
        // блоки через |, внутри блока варианты через ;
        // пример: "cat;kitten|dog;puppy|bird"
        //          блок0: cat ИЛИ kitten
        //          блок1: dog ИЛИ puppy
        //          блок2: bird
        var correctBlocks = element.CorrectAnswer
            .Split('|', StringSplitOptions.None)
            .Select(block => block
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim().ToLower())
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList())
            .ToList();

        if (answers.TryGetValue(element.Id, out var answer) && !string.IsNullOrWhiteSpace(answer))
        {
            grade.YourAnswer = answer;

            // Ответы студента: "слово1|слово2|..." (мы так собираем в SubmitTest)
            var userBlanks = answer
                .Split('|', StringSplitOptions.None)
                .Select(b => b.Trim().ToLower())
                .ToList();

            int correct = 0;

            for (int i = 0; i < Math.Min(correctBlocks.Count, userBlanks.Count); i++)
            {
                var user = userBlanks[i];
                if (string.IsNullOrEmpty(user))
                    continue;

                var acceptable = correctBlocks[i];

                // если блок пустой (на всякий случай) — пропускаем
                if (!acceptable.Any())
                    continue;

                if (acceptable.Contains(user))
                    correct++;
            }

            if (correctBlocks.Count > 0)
            {
                if (correct == correctBlocks.Count)
                {
                    grade.IsCorrect = true;
                    grade.PointsEarned = maxPoints;
                }
                else if (correct > 0)
                {
                    grade.IsPartial = true;
                    grade.PointsEarned = maxPoints * correct / correctBlocks.Count;
                }
            }
        }

        return grade;
    }
    private QuestionGrade GradeQuestion(TestElement element, Dictionary<int, string> answers, 
        Dictionary<int, List<string>> multiAnswers, double maxPoints)
    {
        var grade = new QuestionGrade
        {
            ElementId = element.Id,
            MaxPoints = maxPoints,
            CorrectAnswer = element.CorrectAnswer
        };

        switch (element.Type)
        {
            case ElementType.TextInput:
                // Если в CorrectAnswer есть |, считаем что это многоблочный TextInput
                if (!string.IsNullOrEmpty(element.CorrectAnswer) && element.CorrectAnswer.Contains('|'))
                    grade = GradeTextInputBlanks(element, answers, maxPoints);
                else
                    grade = GradeSimpleAnswer(element, answers, maxPoints);
                break;

            case ElementType.Dropdown:
            case ElementType.RadioButton:
                grade = GradeSimpleAnswer(element, answers, maxPoints);
                break;

            case ElementType.TrueFalse:
                grade = GradeTrueFalse(element, answers, maxPoints);
                break;
                
            case ElementType.Checkbox:
                grade = GradeMultipleChoice(element, multiAnswers, maxPoints);
                break;
                
            case ElementType.NumericRange:
                grade = GradeNumericRange(element, answers, maxPoints);
                break;
                
            case ElementType.Matching:
                grade = GradeMatching(element, answers, maxPoints);
                break;
                
            case ElementType.Ordering:
                grade = GradeOrdering(element, answers, maxPoints);
                break;
                
            case ElementType.FillBlanks:
                grade = GradeFillBlanks(element, answers, maxPoints);
                break;
        }

        // Применяем штраф за неправильный ответ
        if (!grade.IsCorrect && !grade.IsPartial && element.PenaltyPercent > 0)
        {
            grade.PointsEarned = -maxPoints * element.PenaltyPercent / 100.0;
        }

        return grade;
    }

    private QuestionGrade GradeSimpleAnswer(TestElement element, Dictionary<int, string> answers, double maxPoints)
    {
        var grade = new QuestionGrade
        {
            ElementId = element.Id,
            MaxPoints = maxPoints,
            CorrectAnswer = element.CorrectAnswer
        };

        if (answers.TryGetValue(element.Id, out var answer))
        {
            grade.YourAnswer = answer ?? "";
            grade.IsCorrect = string.Equals(answer?.Trim(), element.CorrectAnswer?.Trim(), 
                StringComparison.OrdinalIgnoreCase);
            grade.PointsEarned = grade.IsCorrect ? maxPoints : 0;
        }

        return grade;
    }

    private QuestionGrade GradeTrueFalse(TestElement element, Dictionary<int, string> answers, double maxPoints)
    {
        return GradeSimpleAnswer(element, answers, maxPoints);
    }

    private QuestionGrade GradeMultipleChoice(TestElement element, Dictionary<int, List<string>> multiAnswers, double maxPoints)
    {
        var grade = new QuestionGrade
        {
            ElementId = element.Id,
            MaxPoints = maxPoints,
            CorrectAnswer = element.CorrectAnswer
        };

        var correctAnswers = element.CorrectAnswer
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(a => a.Trim().ToLower())
            .ToHashSet();

        if (multiAnswers.TryGetValue(element.Id, out var selectedAnswers) && selectedAnswers.Any())
        {
            grade.YourAnswer = string.Join(", ", selectedAnswers);
            var selected = selectedAnswers.Select(a => a.Trim().ToLower()).ToHashSet();
            
            int correctSelected = selected.Intersect(correctAnswers).Count();
            int wrongSelected = selected.Except(correctAnswers).Count();
            int missed = correctAnswers.Except(selected).Count();

            if (correctSelected == correctAnswers.Count && wrongSelected == 0)
            {
                grade.IsCorrect = true;
                grade.PointsEarned = maxPoints;
            }
            else if (element.AllowPartialCredit && correctSelected > 0)
            {
                grade.IsPartial = true;
                double partialScore = (double)correctSelected / correctAnswers.Count;
                partialScore -= (double)wrongSelected / correctAnswers.Count * 0.5; // Штраф за лишние
                grade.PointsEarned = Math.Max(0, maxPoints * partialScore);
            }
        }

        return grade;
    }

    private QuestionGrade GradeNumericRange(TestElement element, Dictionary<int, string> answers, double maxPoints)
    {
        var grade = new QuestionGrade
        {
            ElementId = element.Id,
            MaxPoints = maxPoints,
            CorrectAnswer = element.CorrectAnswer
        };

        if (answers.TryGetValue(element.Id, out var answer) && 
            double.TryParse(answer?.Replace(',', '.'), System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out var userValue) &&
            double.TryParse(element.CorrectAnswer?.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var correctValue))
        {
            grade.YourAnswer = answer ?? "";
            double tolerance = element.Tolerance ?? 0;
            
            if (Math.Abs(userValue - correctValue) <= tolerance)
            {
                grade.IsCorrect = true;
                grade.PointsEarned = maxPoints;
            }
        }

        return grade;
    }

    private QuestionGrade GradeMatching(TestElement element, Dictionary<int, string> answers, double maxPoints)
    {
        var grade = new QuestionGrade
        {
            ElementId = element.Id,
            MaxPoints = maxPoints,
            CorrectAnswer = element.CorrectAnswer
        };

        // Формат правильного ответа и ответа студента: "1=1|2=2|3=3" (левый индекс = правый индекс)
        var correctPairs = element.CorrectAnswer
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim().ToLower())
            .ToHashSet();

        if (answers.TryGetValue(element.Id, out var answer) && !string.IsNullOrEmpty(answer))
        {
            grade.YourAnswer = answer;
            var userPairs = answer
                .Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim().ToLower())
                .ToHashSet();

            int correct = userPairs.Intersect(correctPairs).Count();
            
            if (correct == correctPairs.Count)
            {
                grade.IsCorrect = true;
                grade.PointsEarned = maxPoints;
            }
            else if (element.AllowPartialCredit && correct > 0)
            {
                grade.IsPartial = true;
                grade.PointsEarned = maxPoints * correct / correctPairs.Count;
            }
        }

        return grade;
    }

    private QuestionGrade GradeOrdering(TestElement element, Dictionary<int, string> answers, double maxPoints)
    {
        var grade = new QuestionGrade
        {
            ElementId = element.Id,
            MaxPoints = maxPoints,
            CorrectAnswer = element.CorrectAnswer
        };

        // Формат: "1,2,3,4" - правильный порядок индексов
        var correctOrder = element.CorrectAnswer
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToList();

        if (answers.TryGetValue(element.Id, out var answer) && !string.IsNullOrEmpty(answer))
        {
            grade.YourAnswer = answer;
            var userOrder = answer
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToList();

            if (correctOrder.SequenceEqual(userOrder))
            {
                grade.IsCorrect = true;
                grade.PointsEarned = maxPoints;
            }
            else if (element.AllowPartialCredit)
            {
                // Подсчёт количества элементов на правильных позициях
                int correctPositions = 0;
                for (int i = 0; i < Math.Min(correctOrder.Count, userOrder.Count); i++)
                {
                    if (correctOrder[i] == userOrder[i]) correctPositions++;
                }
                
                if (correctPositions > 0)
                {
                    grade.IsPartial = true;
                    grade.PointsEarned = maxPoints * correctPositions / correctOrder.Count;
                }
            }
        }

        return grade;
    }

    private QuestionGrade GradeFillBlanks(TestElement element, Dictionary<int, string> answers, double maxPoints)
    {
        var grade = new QuestionGrade
        {
            ElementId = element.Id,
            MaxPoints = maxPoints,
            CorrectAnswer = element.CorrectAnswer
        };

        // Формат правильных ответов: "слово1|слово2|слово3" для каждого пропуска
        var correctBlanks = element.CorrectAnswer
            .Split('|', StringSplitOptions.None)
            .Select(b => b.Trim().ToLower())
            .ToList();

        if (answers.TryGetValue(element.Id, out var answer) && !string.IsNullOrWhiteSpace(answer))
        {
            grade.YourAnswer = answer;

            // Ответы студента: "слово1|слово2|-|..." (из drag&drop)
            var userBlanks = answer
                .Split('|', StringSplitOptions.None)
                .Select(b => b.Trim().ToLower())
                .ToList();

            int correct = 0;

            // Считаем только совпадения по позициям; "-" и пустые считаем просто «нет ответа»
            for (int i = 0; i < Math.Min(correctBlanks.Count, userBlanks.Count); i++)
            {
                var user = userBlanks[i];

                if (string.IsNullOrEmpty(user) || user == "-")
                    continue;

                if (correctBlanks[i] == user)
                    correct++;
            }

            if (correct == correctBlanks.Count && correctBlanks.Count > 0)
            {
                grade.IsCorrect = true;
                grade.PointsEarned = maxPoints;
            }
            else if (element.AllowPartialCredit && correct > 0 && correctBlanks.Count > 0)
            {
                grade.IsPartial = true;
                grade.PointsEarned = maxPoints * correct / correctBlanks.Count;
            }
            // иначе PointsEarned остаётся 0
        }

        return grade;
    }
}
