import commandBase = require("commands/commandBase");
import database = require("models/resources/database");
import endpoints = require("endpoints");

class backupNowPeriodicCommand extends commandBase {
    private db: database;

    private taskId: number;

    private isFullBackup: boolean;

    private taskName: string;

    constructor(db: database, taskId: number, isFullBackup: boolean, taskName: string) {
        super();
        this.taskName = taskName;
        this.isFullBackup = isFullBackup;
        this.taskId = taskId;
        this.db = db;
    }
 
    execute(): JQueryPromise<Raven.Client.Documents.Operations.Backups.StartBackupOperationResult> {
        const url = endpoints.databases.ongoingTasks.adminBackupDatabase +
            this.urlEncodeArgs({
                taskId: this.taskId,
                isFullBackup: this.isFullBackup
            });

        return this.post(url, null, this.db)
            .fail(response => {
                this.reportError(`Failed to start a backup for task: '${this.taskName}'`,
                    response.responseText,
                    response.statusText);
            });
    }
}

export = backupNowPeriodicCommand; 

