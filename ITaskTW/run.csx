#r "System.Configuration"
#r "System.Data"

using System;
using System.Configuration;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Tweetinvi;
using Tweetinvi.Models;

class Account
{
    Guid AccID { get; set; }
    string UserName { get; set; }
    string UserID { get; set; }
    string FullName { get; set; }
}

public static void Run(TimerInfo timer, TraceWriter log)
{
    IEnumerable<IUser> UserIDs;
    Dictionary<long, Account> Accounts = new Dictionary<long, Account>();
    using (SqlConnection dbConn = new SqlConnection())
    {
        dbConn.ConnectionString = ConfigurationManager.ConnectionStrings["db"].ConnectionString;
        dbConn.Open();

        SqlCommand RetrieveTWAccounts =
            new SqlCommand(@"
            SELECT TOP(100) [AccID], [UserName], [UserID], [FullName] FROM UAccounts Acc
            INNER JOIN (
                SELECT TOP(100) [AccID], MAX([Run]) AS [LatestRun] FROM UData
                GROUP BY [AccID]
                ORDER BY [LatestRun] ASC
            ) AS Prio ON Prio.[AccID] = Acc.[AccID]
            WHERE
                Acc.[Removed] = 0
                AND Acc.[Network] = 'twitter'
                AND CONVERT(DATE, Prio.[LatestRun]) < CONVERT(DATE, GETUTCDATE())
            ORDER BY Prio.[LatestRun] ASC", dbConn);

        using (SqlDataReader results = RetrieveTWAccounts.ExecuteReader())
        {
            if (results.HasRows)
            {
                while (results.Read())
                {
                    Accounts.Add(long.Parse((string)results["UserID"]),
                        new Account() {
                            AccID = (Guid)results["AccID"],
                            UserName = (string)results["UserName"],
                            FullName = (string)results["FullName"]
                        }
                    );
                }
            }
            else
            {
                log.Info("No twitter acounts need checking.");
                return;
            }
        }

        UserIDs = new List<long>(Accounts.Keys);
        var TWCredentials = new TwitterCredentials(
            "pPeCNuIbXnnXNE2gpLhf2M3IJ",
            "GKGipEoIWmJGg6o8kI8BzV04R3LjxinNOKl7aSkjwQnHR2Bzr8",
            "729417297996095488-3Rs0M9XyiS4YulJpG9kMwnvj5dqEmLR",
            "uqNWQNjNDHxvKrH5l3gQg47NedGzYGi97C4tpxaCYbDrA");
        
        try
        {
            Users = Auth.ExecuteOperationWithCredentials(TWCredentials, () =>
            {
                return Tweetinvi.User.GetUsersFromIDs(UserIDs);
            });

            foreach (IUser User in Users)
            {
                Console.WriteLine(String.Format("{0} (@{1}) ID: {2}", User.Name, User.ScreenName, User.Id));
                Console.WriteLine(String.Format("Following: {0}\t Followers: {1}", User.FriendsCount, User.FollowersCount));

                using(SqlCommand InsertDataRow =
                    new SqlCommand(@"
                    INSERT INTO UData ([AccID], [FollowCount], [FollowerCount]) VALUES (@AccID, @FollowCount, @FollowerCount)", dbConn))
                {
                    InsertDataRow.Parameters.Add("@AccID", SqlDbType.UniqueIdentifier).Value = new Guid((string)acc.SelectToken("AccID"));
                    InsertDataRow.Parameters.Add("@FollowCount", SqlDbType.Int).Value = User.FriendsCount;
                    InsertDataRow.Parameters.Add("@FollowerCount", SqlDbType.Int).Value = User.FollowersCount;
                    
                    InsertDataRow.ExecuteNonQuery();
                }
            }
        }
        catch
        {
            log.Error("Failed to connect to Twitter API");
        }
    }

    return;
}