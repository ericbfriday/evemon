using System.Collections.Generic;
using System.Linq;
using EVEMon.Common.CustomEventArgs;
using EVEMon.Common.Serialization.API;

namespace EVEMon.Common
{
    public sealed class CorporationDataQuerying
    {
        #region Fields

        private readonly CorporationQueryMonitor<SerializableAPIMarketOrders> m_corpMarketOrdersMonitor;
        private readonly CorporationQueryMonitor<SerializableAPIIndustryJobs> m_corpIndustryJobsMonitor;
        private readonly List<IQueryMonitorEx> m_corporationQueryMonitors;
        private readonly CCPCharacter m_ccpCharacter;

        #endregion


        #region Constructor

        public CorporationDataQuerying(CCPCharacter ccpCharacter)
        {
            m_ccpCharacter = ccpCharacter;
            m_corporationQueryMonitors = new List<IQueryMonitorEx>();

            // Initializes the query monitors 
            m_corpMarketOrdersMonitor =
                new CorporationQueryMonitor<SerializableAPIMarketOrders>(ccpCharacter,
                                                                         APICorporationMethods.CorporationMarketOrders);
            m_corpMarketOrdersMonitor.Updated += OnCorporationMarketOrdersUpdated;
            m_corporationQueryMonitors.Add(m_corpMarketOrdersMonitor);

            m_corpIndustryJobsMonitor =
                new CorporationQueryMonitor<SerializableAPIIndustryJobs>(ccpCharacter,
                                                                         APICorporationMethods.CorporationIndustryJobs);
            m_corpIndustryJobsMonitor.Updated += OnCorporationIndustryJobsUpdated;
            m_corporationQueryMonitors.Add(m_corpIndustryJobsMonitor);

            m_corporationQueryMonitors.ForEach(monitor => ccpCharacter.QueryMonitors.Add(monitor));

            EveMonClient.CharacterListUpdated += EveMonClient_CharacterListUpdated;
        }


        #endregion


        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether [corp orders updated].
        /// </summary>
        /// <value><c>true</c> if [corp orders updated]; otherwise, <c>false</c>.</value>
        internal bool CorpOrdersUpdated { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether [corp orders added].
        /// </summary>
        /// <value><c>true</c> if [corp orders added]; otherwise, <c>false</c>.</value>
        internal bool CorpOrdersAdded { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether [corp jobs updated].
        /// </summary>
        /// <value><c>true</c> if [corp jobs updated]; otherwise, <c>false</c>.</value>
        internal bool CorpJobsUpdated { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether [corp jobs added].
        /// </summary>
        /// <value><c>true</c> if [corp jobs added]; otherwise, <c>false</c>.</value>
        internal bool CorpJobsAdded { get; set; }

        #endregion


        #region Querying

        /// <summary>
        /// Processes the queried character's corporation market orders.
        /// </summary>
        /// <param name="result"></param>
        /// <remarks>This method is sensitive to which market orders gets queried first</remarks>
        private void OnCorporationMarketOrdersUpdated(APIResult<SerializableAPIMarketOrders> result)
        {
            CorpOrdersUpdated = true;

            // Notify an error occurred
            if (m_ccpCharacter.ShouldNotifyError(result, APICorporationMethods.CorporationMarketOrders))
                EveMonClient.Notifications.NotifyCorporationMarketOrdersError(m_ccpCharacter, result);

            CharacterDataQuerying characterDataQuerying = m_ccpCharacter.CharacterDataQuerying;

            // Add orders to list
            CorpOrdersAdded = m_ccpCharacter.AddOrders(result, characterDataQuerying.CharOrdersAdded, IssuedFor.Corporation);

            // If character can not query character data, we switch the char orders updated flag
            // and proceed with the orders importation
            IQueryMonitor characterMarketOrdersMonitor = m_ccpCharacter.QueryMonitors[APICharacterMethods.MarketOrders];
            characterDataQuerying.CharOrdersUpdated |= characterMarketOrdersMonitor == null ||
                                                       !characterMarketOrdersMonitor.Enabled;

            // Import the data if all queried
            if (!characterDataQuerying.CharOrdersUpdated)
                return;

            m_ccpCharacter.ImportOrders();

        }

        /// <summary>
        /// Processes the queried character's corporation industry jobs.
        /// </summary>
        /// <param name="result"></param>
        /// <remarks>This method is sensitive to which "issued for" jobs gets queried first</remarks>
        private void OnCorporationIndustryJobsUpdated(APIResult<SerializableAPIIndustryJobs> result)
        {
            CorpJobsUpdated = true;

            // Notify an error occurred
            if (m_ccpCharacter.ShouldNotifyError(result, APICorporationMethods.CorporationIndustryJobs))
                EveMonClient.Notifications.NotifyCorporationIndustryJobsError(m_ccpCharacter, result);

            CharacterDataQuerying characterDataQuerying = m_ccpCharacter.CharacterDataQuerying;

            // Add jobs to list
            CorpJobsAdded = m_ccpCharacter.AddJobs(result, characterDataQuerying.CharJobsAdded, IssuedFor.Corporation);

            // If character can not query character data, we switch the char jobs updated flag
            // and proceed with the jobs importation
            IQueryMonitor charIndustryJobsMonitor = m_ccpCharacter.QueryMonitors[APICharacterMethods.IndustryJobs];
            characterDataQuerying.CharJobsUpdated |= charIndustryJobsMonitor == null || !charIndustryJobsMonitor.Enabled;

            // Import the data if all queried
            if (!characterDataQuerying.CharJobsUpdated)
                return;

            m_ccpCharacter.ImportJobs();
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the CharacterListUpdated event of the EveMonClient control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EVEMon.Common.CustomEventArgs.APIKeyInfoChangedEventArgs"/> instance containing the event data.</param>
        private void EveMonClient_CharacterListUpdated(object sender, APIKeyInfoChangedEventArgs e)
        {
            if (!m_ccpCharacter.Identity.APIKeys.Contains(e.APIKey))
                return;

            if ((e.APIKey.Type == APIKeyType.Account || e.APIKey.Type == APIKeyType.Character) &&
                m_ccpCharacter.Identity.APIKeys.All(
                    apiKey => apiKey.Type == APIKeyType.Account || apiKey.Type == APIKeyType.Character) &&
                m_corporationQueryMonitors.Exists(monitor => m_ccpCharacter.QueryMonitors.Contains(monitor)))
            {
                m_corporationQueryMonitors.ForEach(monitor => m_ccpCharacter.QueryMonitors.Remove(monitor));
                return;
            }

            if (e.APIKey.Type != APIKeyType.Corporation ||
                m_corporationQueryMonitors.Exists(monitor => m_ccpCharacter.QueryMonitors.Contains(monitor)))
                return;

            m_corporationQueryMonitors.ForEach(monitor => m_ccpCharacter.QueryMonitors.Add(monitor));
        }

        #endregion
    }
}